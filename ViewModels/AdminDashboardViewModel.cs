using ECommerceSite.Models;

namespace ECommerceSite.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TodayOrdersCount { get; set; }
        public int NewOrdersCount { get; set; }
        public int AcceptedOrdersCount { get; set; }
        public int DeliveredOrdersCount { get; set; }
        public int CancelledOrdersCount { get; set; }
        public decimal TodaySales { get; set; }
        public List<Order> RecentOrders { get; set; } = new();
    }
}
