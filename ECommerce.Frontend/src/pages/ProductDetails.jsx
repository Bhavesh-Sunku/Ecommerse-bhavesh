import {
  useEffect,
  useState
} from "react";

import {
  Link,
  useNavigate,
  useParams
} from "react-router-dom";

import {
  getProductById
} from "../services/productService";

import { addCartItem } from "../services/cartService";

import { useAuth } from "../hooks/useAuth";

import Loading from "../components/Loading";
import ErrorMessage from "../components/ErrorMessage";
import { formatINR } from "../utils/currency";

function ProductDetails() {
  const { id } = useParams();

  const navigate = useNavigate();

  const { isLoggedIn } = useAuth();

  const [quantity, setQuantity] = useState(1);
  const [adding, setAdding] = useState(false);
  const [productResult, setProductResult] = useState({
    id: null,
    product: null,
    error: "",
    loading: true
  });

  useEffect(() => {
    let active = true;

    async function loadProduct() {
      try {
        const productData = await getProductById(id);
        if (active) {
          setProductResult({
            id,
            product: productData,
            error: "",
            loading: false
          });
        }
      } catch (requestError) {
        if (active) {
          setProductResult({
            id,
            product: null,
            error: requestError.message || "Unable to load product.",
            loading: false
          });
        }
      }
    }

    loadProduct();
    return () => {
      active = false;
    };
  }, [id]);

  const isCurrentProduct = productResult.id === id;
  const product = isCurrentProduct ? productResult.product : null;
  const error = isCurrentProduct ? productResult.error : "";
  const loading = !isCurrentProduct || productResult.loading;

  async function handleAddToCart() {
    if (!isLoggedIn) {
      navigate("/login");
      return;
    }

    const availableStock = product?.availableStock ?? 0;
    if (quantity > availableStock) {
      setProductResult((current) => ({ ...current, error: "Insufficient stock." }));
      return;
    }

    try {
      setAdding(true);
      setProductResult((current) => ({ ...current, error: "" }));
      await addCartItem(Number(id), Number(quantity));
      navigate("/cart");
    } catch (requestError) {
      setProductResult((current) => ({
        ...current,
        error: requestError.message || "Unable to add this product."
      }));
    } finally {
      setAdding(false);
    }
  }

  if (loading) {
    return (
      <Loading message="Loading product..." />
    );
  }

  if (error && !product) {
    return (
      <ErrorMessage message={error} />
    );
  }

  if (!product) {
    return <p>Product not found.</p>;
  }

  return (
    <div className="details-card">

      {product.imageUrl && (
        <img className="details-image" src={product.imageUrl} alt={product.name} />
      )}
      <h1>{product.name}</h1>

      <p>
        {product.description}
      </p>

      <h2>{formatINR(product.price)}</h2>

      <p>
        Category: {product.category}
      </p>

      <p>
        Available Stock:{" "}
        {product.availableStock ?? 0}
      </p>

      {error && (
        <ErrorMessage message={error} />
      )}

      <label htmlFor="product-quantity">
        Quantity
      </label>

      <input
        id="product-quantity"
        className="quantity-input"
        type="number"
        min="1"
        max={Math.min(10, product.availableStock || 1)}
        value={quantity}
        onChange={(event) => {
          const nextQuantity = Number(event.target.value);
          if (
            Number.isInteger(nextQuantity) &&
            nextQuantity >= 1 &&
            nextQuantity <= Math.min(10, product.availableStock || 1)
          ) {
            setQuantity(nextQuantity);
          }
        }}
      />

      <br />

      <button
        onClick={handleAddToCart}
        disabled={
          adding ||
          !product.availableStock
        }
      >
        {adding ? "Adding..." : "Add to Cart"}
      </button>
      <Link className="button secondary" to="/products">Back to products</Link>

    </div>
  );
}

export default ProductDetails;