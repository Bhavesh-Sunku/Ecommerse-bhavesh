import {
  useEffect,
  useState
} from "react";

import {
  Link
} from "react-router-dom";

import {
  getMyOrders
} from "../services/orderService";

import Loading from "../components/Loading";
import ErrorMessage from "../components/ErrorMessage";
import { formatINR } from "../utils/currency";

function Orders() {
  const [orders, setOrders] = useState([]);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");

  useEffect(() => {
    let active = true;

    async function loadOrders() {
      try {
        const data = await getMyOrders();
        if (active) {
          setOrders(
            Array.isArray(data)
              ? data
              : data?.items || []
          );
        }
      } catch (requestError) {
        if (active) {
          setError(requestError.message || "Unable to load your orders.");
        }
      } finally {
        if (active) {
          setLoading(false);
        }
      }
    }

    loadOrders();
    return () => {
      active = false;
    };
  }, []);

  if (loading) {
    return (
      <Loading message="Loading orders..." />
    );
  }

  return (
    <div>

      <h1>My Orders</h1>

      <ErrorMessage
        message={error}
      />

      {orders.length === 0 ? (
        <p>
          You have no orders.
        </p>
      ) : (
        <div className="orders-list">

          {orders.map((order) => (
            <div
              className="order-card"
              key={order.id}
            >
              <h2>
                Order #{order.id}
              </h2>

              <p>
                Date: {order.orderDate ? new Date(order.orderDate).toLocaleDateString() : "Unavailable"}
              </p>

              <p>Total: {formatINR(order.totalAmount)}</p>

              <p>
                Status:{" "}
                {order.orderStatus ||
                  order.status}
              </p>

              <Link
                className="button"
                to={`/orders/${order.id}`}
              >
                View Order
              </Link>
            </div>
          ))}

        </div>
      )}

    </div>
  );
}

export default Orders;