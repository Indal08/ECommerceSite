namespace ECommerceSite.Services
{
    public class RazorpayService : IRazorpayService
    {
        public RazorpayService(IConfiguration config)
        {
        }

        public (string razorpayOrderId, string keyId) CreateOrder(decimal amount, string receipt)
        {
            throw new NotSupportedException("Razorpay is disabled for this COD-only MVP. No online payment is required.");
        }

        public bool VerifyPayment(string razorpayOrderId, string razorpayPaymentId, string razorpaySignature)
        {
            return false;
        }
    }
}
