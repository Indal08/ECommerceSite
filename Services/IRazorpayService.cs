namespace ECommerceSite.Services
{
    public interface IRazorpayService
    {
        (string razorpayOrderId, string keyId) CreateOrder(decimal amount, string receipt);
        bool VerifyPayment(string razorpayOrderId, string razorpayPaymentId, string razorpaySignature);
    }
}
