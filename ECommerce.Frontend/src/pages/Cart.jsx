import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import Loading from "../components/Loading";
import ErrorMessage from "../components/ErrorMessage";
import { formatINR } from "../utils/currency";
import {
  clearCart,
  getCart,
  removeCartItem,
  updateCartItem
} from "../services/cartService";

function Cart() {
  const [cart, setCart] = useState(null);
  const [loading, setLoading] = useState(true);
  const [updating, setUpdating] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    let active = true;

    async function loadCart() {
      try {
        const data = await getCart();
        if (active) {
          setCart(data);
        }
      } catch (requestError) {
        if (active) {
          setError(requestError.message || "Unable to load your cart.");
        }
      } finally {
        if (active) {
          setLoading(false);
        }
      }
    }

    loadCart();
    return () => {
      active = false;
    };
  }, []);

  async function applyCartChange(operation) {
    try {
      setUpdating(true);
      setError("");
      setCart(await operation());
    } catch (requestError) {
      setError(requestError.message || "Unable to update your cart.");
    } finally {
      setUpdating(false);
    }
  }

  if (loading) {
    return <Loading message="Loading cart..." />;
  }

  const items = cart?.items || [];
  const total = Number(cart?.totalAmount ?? 0);

  return (
    <section>
      <div className="page-header">
        <h1>Your Shopping Cart</h1>
      </div>
      <ErrorMessage message={error} />

      {items.length === 0 ? (
        <div className="panel">
          <p>Your cart is empty.</p>
          <Link className="button" to="/products">Continue shopping</Link>
        </div>
      ) : (
        <>
          <div className="cart-items">
            {items.map((item) => (
              <article className="cart-item" key={item.id}>
                {item.imageUrl && (
                  <img src={item.imageUrl} alt={item.productName} />
                )}
                <div className="cart-item-info">
                  <h2>{item.productName}</h2>
                  <p>{formatINR(item.unitPrice)} each</p>
                  <p>Available: {item.availableStock}</p>
                </div>
                <div className="cart-quantity" aria-label="Quantity controls">
                  <button
                    type="button"
                    aria-label={`Decrease ${item.productName} quantity`}
                    disabled={updating || item.quantity <= 1}
                    onClick={() => applyCartChange(() => updateCartItem(item.id, item.quantity - 1))}
                  >
                    −
                  </button>
                  <span>{item.quantity}</span>
                  <button
                    type="button"
                    aria-label={`Increase ${item.productName} quantity`}
                    disabled={updating || item.quantity >= item.availableStock}
                    onClick={() => applyCartChange(() => updateCartItem(item.id, item.quantity + 1))}
                  >
                    +
                  </button>
                </div>
                <strong>{formatINR(item.subtotal)}</strong>
                <button
                  className="secondary"
                  type="button"
                  disabled={updating}
                  onClick={() => applyCartChange(() => removeCartItem(item.id))}
                >
                  Remove
                </button>
              </article>
            ))}
          </div>
          <div className="cart-summary">
            <button
              className="secondary"
              type="button"
              disabled={updating}
              onClick={() => applyCartChange(clearCart)}
            >
              Clear Cart
            </button>
            <Link className="button secondary" to="/products">Continue shopping</Link>
            <strong>Total: {formatINR(total)}</strong>
            <Link className="button" to="/checkout">Proceed to Checkout</Link>
          </div>
        </>
      )}
    </section>
  );
}

export default Cart;
