import { useCallback, useEffect, useRef, useState } from "react";

import {
  getProducts,
  getProductCategories
} from "../services/productService";

import ProductCard from "../components/ProductCard";
import Loading from "../components/Loading";
import ErrorMessage from "../components/ErrorMessage";

function Products() {
  const [products, setProducts] = useState([]);

  const [search, setSearch] = useState("");
  const [category, setCategory] = useState("");
  const [minPrice, setMinPrice] = useState("");
  const [maxPrice, setMaxPrice] = useState("");
  const [sortBy, setSortBy] = useState("newest");
  const [sortDescending, setSortDescending] = useState(true);
  const [pageNumber, setPageNumber] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [categories, setCategories] = useState([]);

  const [loading, setLoading] = useState(true);

  const [error, setError] = useState("");
  const requestId = useRef(0);
  const filters = useRef({});
  const loadProducts = useCallback(async (page = 1, overrides = {}) => {
    const currentRequestId = ++requestId.current;

    try {
      const data = await getProducts({
        ...filters.current,
        ...overrides,
        pageNumber: page,
        pageSize: 12
      });

      const productList =
        Array.isArray(data)
          ? data
          : data?.items ||
            data?.products ||
            [];

      if (currentRequestId === requestId.current) {
        setProducts(productList);
        setPageNumber(data?.pageNumber || page);
        setTotalPages(data?.totalPages || 1);
      }
    } catch (error) {
      if (currentRequestId === requestId.current) {
        setError(error.message || "Unable to load products.");
      }
    } finally {
      if (currentRequestId === requestId.current) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    filters.current = {
      search,
      category,
      minPrice,
      maxPrice,
      sortBy,
      sortDescending,
      includeOutOfStock: true
    };
  }, [category, maxPrice, minPrice, search, sortBy, sortDescending]);

  useEffect(() => {
    loadProducts();
    getProductCategories()
      .then(setCategories)
      .catch((requestError) => setError(requestError.message));
  }, [loadProducts]);

  function handleSearch(event) {
    event.preventDefault();
    refreshProducts(1);
  }

  function refreshProducts(page, overrides) {
    setLoading(true);
    setError("");
    loadProducts(page, overrides);
  }

  return (
    <div>

      <div className="page-header">
        <div>
          <h1>Products</h1>
          <p>
            Browse available products.
          </p>
        </div>
      </div>

      <form
        className="search-form"
        onSubmit={handleSearch}
      >
        <input
          value={search}
          onChange={(event) =>
            setSearch(event.target.value)
          }
          placeholder="Search products..."
        />

        <button type="submit">
          Search
        </button>
      </form>

      <div className="catalog-filters">
        <label>
          Category
          <select
            value={category}
            onChange={(event) => {
              const value = event.target.value;
              setCategory(value);
              refreshProducts(1, { category: value });
            }}
          >
            <option value="">All categories</option>
            {categories.map((value) => <option key={value} value={value}>{value}</option>)}
          </select>
        </label>
        <label>
          Minimum price
          <input
            type="number"
            min="0"
            value={minPrice}
            onChange={(event) => setMinPrice(event.target.value)}
            onBlur={() => refreshProducts(1)}
          />
        </label>
        <label>
          Maximum price
          <input
            type="number"
            min="0"
            value={maxPrice}
            onChange={(event) => setMaxPrice(event.target.value)}
            onBlur={() => refreshProducts(1)}
          />
        </label>
        <label>
          Sort by
          <select
            value={`${sortBy}-${sortDescending ? "desc" : "asc"}`}
            onChange={(event) => {
              const [nextSortBy, direction] = event.target.value.split("-");
              setSortBy(nextSortBy);
              setSortDescending(direction === "desc");
              refreshProducts(1, {
                sortBy: nextSortBy,
                sortDescending: direction === "desc"
              });
            }}
          >
            <option value="newest-desc">Newest</option>
            <option value="price-asc">Price: Low to high</option>
            <option value="price-desc">Price: High to low</option>
            <option value="name-asc">Name: A to Z</option>
            <option value="name-desc">Name: Z to A</option>
          </select>
        </label>
      </div>

      <ErrorMessage message={error} />

      {loading ? (
        <Loading message="Loading products..." />
      ) : (
        <div className="product-grid">

          {products.length === 0 ? (
            <p>
              No products found.
            </p>
          ) : (
            products.map((product) => (
              <ProductCard
                key={product.id}
                product={product}
              />
            ))
          )}

        </div>
      )}

      {totalPages > 1 && (
        <div className="pagination">
          <button type="button" disabled={loading || pageNumber <= 1} onClick={() => refreshProducts(pageNumber - 1)}>
            Previous
          </button>
          <span>Page {pageNumber} of {totalPages}</span>
          <button type="button" disabled={loading || pageNumber >= totalPages} onClick={() => refreshProducts(pageNumber + 1)}>
            Next
          </button>
        </div>
      )}

    </div>
  );
}

export default Products;