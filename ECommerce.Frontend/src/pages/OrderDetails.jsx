import {
  useEffect,
  useState
} from "react";

import {
  Link,
  useParams
} from "react-router-dom";

import {
  getOrderById,
  cancelOrder
} from "../services/orderService";

import Loading from "../components/Loading";
import ErrorMessage from "../components/ErrorMessage";
import { formatINR } from "../utils/currency";

function OrderDetails() {
  const { id } = useParams();

  const [orderResult, setOrderResult] = useState({
    requestKey: null,
    order: null,
    error: "",
    loading: true
  });
  const [reloadCount, setReloadCount] = useState(0);
  const [cancelError, setCancelError] = useState("");
  const [cancelling, setCancelling] = useState(false);
  const requestKey = `${id}:${reloadCount}`;

  useEffect(() => {
    let active = true;

    async function loadOrder() {
      try {
        const data = await getOrderById(id);
        if (active) {
          setOrderResult({
            requestKey,
            order: data,
            error: "",
            loading: false
          });
        }
      } catch (requestError) {
        if (active) {
          setOrderResult({
            requestKey,
            order: null,
            error: requestError.message || "Unable to load order.",
            loading: false
          });
        }
      }
    }

    loadOrder();
    return () => {
      active = false;
    };
  }, [id, reloadCount, requestKey]);

  const isCurrentOrder = orderResult.requestKey === requestKey;
  const order = isCurrentOrder ? orderResult.order : null;
  const error = isCurrentOrder ? orderResult.error : "";
  const loading = !isCurrentOrder || orderResult.loading;

  async function handleCancel() {
    try {
      setCancelling(true);
      setCancelError("");

      await cancelOrder(id);
      setReloadCount((current) => current + 1);
    } catch (requestError) {
      setCancelError(requestError.message || "Unable to cancel order.");
    } finally {
      setCancelling(false);
    }
  }

  if (loading) {
    return (
      <Loading message="Loading order..." />
    );
  }

  if (!order) {
    return (
      <ErrorMessage
        message={
          error || cancelError || "Order not found."
        }
      />
    );
  }

  const status =
    order.orderStatus ||
    order.status;

  return (
    <div className="details-card">
      {cancelError && <ErrorMessage message={cancelError} />}

      <h1>
        Order #{order.id}
      </h1>

      <p>
        Status: <strong>{status}</strong>
      </p>

      <p>Total: {formatINR(order.totalAmount)}</p>

      {order.shippingAddress && (
        <section>
          <h2>Shipping details</h2>
          <p>{order.customerName}</p>
          <p>{order.shippingAddress}</p>
          <p>{order.phoneNumber}</p>
        </section>
      )}

      <h2>Items</h2>

      <div className="order-items">

        {order.items?.map(
          (item, index) => (
            <div
              className="order-item"
              key={index}
            >
              <p>
                {item.productName}
              </p>

              <p>
                Quantity: {item.quantity}
              </p>

              <p>Unit Price: {formatINR(item.unitPrice)}</p>

              <p>Subtotal: {formatINR(item.subtotal ?? item.subTotal)}</p>
            </div>
          )
        )}

      </div>

      {status === "Pending" && (
        <button
          onClick={handleCancel}
          disabled={cancelling}
        >
          {cancelling
            ? "Cancelling..."
            : "Cancel Order"}
        </button>
      )}

      {status === "Pending" && (
        <Link
          className="button secondary"
          to={`/checkout/${order.id}`}
        >
          Continue to Checkout
        </Link>
      )}

    </div>
  );
}

export default OrderDetails;