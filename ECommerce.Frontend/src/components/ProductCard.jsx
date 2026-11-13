import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../hooks/useAuth";
import { addCartItem } from "../services/cartService";
import { formatINR } from "../utils/currency";

function ProductCard({ product }) {
  const navigate = useNavigate();
  const { isLoggedIn } = useAuth();
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

  async function handleAddToCart() {
    if (!isLoggedIn) {
      navigate("/login");
      return;
    }

    try {
      setMessage("");
      setError("");
      await addCartItem(product.id, 1);
      setMessage("Added to cart.");
    } catch (requestError) {
      setError(requestError.message || "Unable to add this product.");
    }
  }

  return (
    <article className="product-card">
      {product.imageUrl && <img className="product-image" src={product.imageUrl} alt={product.name} />}
      <span className="eyebrow">{product.category || "Product"}</span>
      <h2>{product.name}</h2>
      <p>{product.description || "Quality product available now."}</p>
      <strong>{formatINR(product.price)}</strong>
      <p className={product.availableStock > 0 ? "stock-available" : "stock-unavailable"}>
        {product.availableStock > 0 ? `${product.availableStock} in stock` : "Out of stock"}
      </p>
      <div>
        <Link className="button" to={`/products/${product.id}`}>View Product</Link>
        <button type="button" onClick={handleAddToCart} disabled={product.availableStock === 0}>
          {product.availableStock === 0 ? "Out of Stock" : "Add to Cart"}
        </button>
      </div>
      {message && <p className="success">{message}</p>}
      {error && <p className="error">{error}</p>}
    </article>
  );
}

export default ProductCard;
