namespace ECommerceSite.Models
{
    public static class OrderStatusValidator
    {
        public static bool IsTerminalStatus(OrderStatus status)
        {
            return status is OrderStatus.Cancelled or OrderStatus.Delivered;
        }

        public static bool CanProcessPayment(OrderStatus status)
        {
            return status != OrderStatus.Cancelled;
        }

        public static OrderStatus ApplyPaymentReceived(OrderStatus status)
        {
            return status switch
            {
                OrderStatus.Accepted => OrderStatus.Delivered,
                _ => status
            };
        }

        public static bool IsFullyCompleted(OrderStatus status, PaymentStatus paymentStatus)
        {
            return status == OrderStatus.Delivered && paymentStatus == PaymentStatus.Paid;
        }

        public static bool IsValidTransition(OrderStatus from, OrderStatus to)
        {
            if (from == to)
            {
                return true;
            }

            if (IsTerminalStatus(from))
            {
                return false;
            }

            return (from, to) switch
            {
                (OrderStatus.NewOrder, OrderStatus.Accepted) => true,
                (OrderStatus.NewOrder, OrderStatus.Cancelled) => true,
                (OrderStatus.Accepted, OrderStatus.Delivered) => true,
                _ => false
            };
        }
    }
}
