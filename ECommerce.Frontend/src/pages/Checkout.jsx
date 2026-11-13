import { useEffect, useRef, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useAuth } from "../hooks/useAuth";
import ErrorMessage from "../components/ErrorMessage";
import Loading from "../components/Loading";
import { getCart } from "../services/cartService";
import { createOrderFromCart } from "../services/orderService";
import { getOrderById } from "../services/orderService";
import { createPayment } from "../services/paymentService";
import { formatINR } from "../utils/currency";

function Checkout() {
  const { orderId } = useParams();
  const { user } = useAuth();
  const [cart, setCart] = useState(null);
  const [customerName, setCustomerName] = useState(user?.name || "");
  const [shippingAddress, setShippingAddress] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [placedOrderId, setPlacedOrderId] = useState(null);
  const [existingOrder, setExistingOrder] = useState(null);
  const [payment, setPayment] = useState(null);
  const [paymentError, setPaymentError] = useState("");
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const submittingRef = useRef(false);

  useEffect(() => {
    let active = true;

    async function loadCheckoutData() {
      try {
        const data = orderId
          ? await getOrderById(orderId)
          : await getCart();
        if (active) {
          if (orderId) {
            setExistingOrder(data);
          } else {
            setCart(data);
          }
        }
      } catch (requestError) {
        if (active) {
          setError(requestError.message || "Unable to load your order summary.");
        }
      } finally {
        if (active) {
          setLoading(false);
        }
      }
    }

    loadCheckoutData();
    return () => {
      active = false;
    };
  }, [orderId]);

  function getIdempotencyKey(paymentOrderId) {
    const storageKey = `ecommerce-payment-${paymentOrderId}`;
    const existingKey = sessionStorage.getItem(storageKey);
    if (existingKey) {
      return existingKey;
    }

    const newKey = globalThis.crypto?.randomUUID?.()
      ?? `${Date.now()}-${Math.random().toString(36).slice(2)}`;
    sessionStorage.setItem(storageKey, newKey);
    return newKey;
  }

  async function processPayment(paymentOrderId) {
    setPaymentError("");
    try {
      const result = await createPayment(
        paymentOrderId,
        getIdempotencyKey(paymentOrderId)
      );
      setPayment(result);
      if (result.status === "Failed") {
        sessionStorage.removeItem(`ecommerce-payment-${paymentOrderId}`);
        setPaymentError("The mock payment was declined. You can try the payment again.");
      } else if (result.orderConfirmed) {
        sessionStorage.removeItem(`ecommerce-payment-${paymentOrderId}`);
        setPaymentError("");
      } else {
        setPaymentError("Payment is recorded, but order confirmation is still pending. Retry to complete it.");
      }
    } catch (requestError) {
      if (requestError.status === 402) {
        sessionStorage.removeItem(`ecommerce-payment-${paymentOrderId}`);
        setPaymentError("The mock payment was declined. You can try the payment again.");
      } else {
        setPaymentError(
          requestError.message || "Payment could not be confirmed. Retry safely to continue."
        );
      }
    }
  }

  async function handlePayExistingOrder() {
    const paymentOrderId = existingOrder?.id ?? placedOrderId;
    if (submittingRef.current || !paymentOrderId) {
      return;
    }

    submittingRef.current = true;
    setSubmitting(true);
    try {
      await processPayment(paymentOrderId);
      if (existingOrder) {
        const refreshedOrder = await getOrderById(paymentOrderId);
        setExistingOrder(refreshedOrder);
      }
    } catch (requestError) {
      setPaymentError(requestError.message || "Unable to refresh the order.");
    } finally {
      submittingRef.current = false;
      setSubmitting(false);
    }
  }

  async function handlePlaceOrder(event) {
    event.preventDefault();
    if (
      !customerName.trim() ||
      shippingAddress.trim().length < 10 ||
      !phoneNumber.trim()
    ) {
      setError("Please enter valid shipping details.");
      return;
    }

    if (submittingRef.current) {
      return;
    }

    submittingRef.current = true;
    try {
      setSubmitting(true);
      setError("");
      const order = await createOrderFromCart({
        customerName,
        shippingAddress,
        phoneNumber
      });
      setPlacedOrderId(order.id);
      setCart({ items: [], totalAmount: 0 });
      await processPayment(order.id);
    } catch (requestError) {
      setError(requestError.message || "Unable to place your order.");
    } finally {
      submittingRef.current = false;
      setSubmitting(false);
    }
  }

  if (orderId) {
    return (
      <section className="panel">
        <h1>Complete payment</h1>
        {loading ? <Loading message="Loading order..." /> : null}
        <ErrorMessage message={error} />
        {existingOrder?.status === "Confirmed" ? (
          <p className="success">Order #{orderId} is confirmed and the mock payment succeeded.</p>
        ) : existingOrder ? (
          <>
            <p>Order total: {formatINR(existingOrder.totalAmount)}</p>
            <ErrorMessage message={paymentError} />
            <p>Sandbox only: no card details are requested or stored.</p>
            <button type="button" onClick={handlePayExistingOrder} disabled={submitting}>
              {submitting ? "Processing mock payment..." : "Pay with mock provider"}
            </button>
          </>
        ) : null}
        <Link className="button" to={`/orders/${orderId}`}>View order #{orderId}</Link>
      </section>
    );
  }

  if (placedOrderId) {
    return (
      <section className="panel">
        <h1>Order placed</h1>
        <p className="success">Your order #{placedOrderId} was placed successfully.</p>
        {submitting ? <p>Processing sandbox payment...</p> : null}
        {payment?.orderConfirmed ? (
          <p className="success">Mock payment confirmed. No real payment was taken.</p>
        ) : null}
        <ErrorMessage message={paymentError} />
        {!submitting && !payment?.orderConfirmed ? (
          <button type="button" onClick={() => handlePayExistingOrder()} disabled={submitting}>
            Retry mock payment
          </button>
        ) : null}
        <Link className="button" to={`/orders/${placedOrderId}`}>View order</Link>
      </section>
    );
  }

  if (loading) {
    return <Loading message="Preparing checkout..." />;
  }

  const items = cart?.items || [];
  const total = Number(cart?.totalAmount ?? 0);

  return (
    <section>
      <h1>Checkout</h1>
      <ErrorMessage message={error} />
      {items.length === 0 ? (
        <div className="panel">
          <p>Your cart is empty.</p>
          <Link className="button" to="/products">Continue shopping</Link>
        </div>
      ) : (
        <div className="checkout-layout">
          <form className="form-card" onSubmit={handlePlaceOrder}>
            <label>
              Customer name
              <input
                value={customerName}
                onChange={(event) => setCustomerName(event.target.value)}
                maxLength="200"
                required
              />
            </label>
            <label>
              Shipping address
              <textarea
                value={shippingAddress}
                onChange={(event) => setShippingAddress(event.target.value)}
                minLength="10"
                maxLength="500"
                required
              />
            </label>
            <label>
              Phone number
              <input
                type="tel"
                value={phoneNumber}
                onChange={(event) => setPhoneNumber(event.target.value)}
                maxLength="30"
                required
              />
            </label>
            <button type="submit" disabled={submitting}>
              {submitting ? "Placing order..." : "Place Order"}
            </button>
            <p>Sandbox mock payment only. Do not enter card information.</p>
          </form>
          <aside className="panel">
            <h2>Order summary</h2>
            {items.map((item) => (
              <div className="checkout-line" key={item.id}>
                <span>{item.productName} × {item.quantity}</span>
                <strong>{formatINR(item.subtotal)}</strong>
              </div>
            ))}
            <hr />
            <strong>Total: {formatINR(total)}</strong>
          </aside>
        </div>
      )}
    </section>
  );
}

export default Checkout;