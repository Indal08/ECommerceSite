namespace ECommerceSite.Helpers
{
    public static class CartQuantityValidator
    {
        public const decimal MinQuantity = 0.25m;
        public const decimal MaxQuantity = 50m;
        public const decimal QuantityStep = 0.25m;

        public static bool IsValidQuantity(decimal quantity)
        {
            if (quantity <= 0m || quantity > MaxQuantity)
                return false;

            return quantity % QuantityStep == 0m;
        }
    }
}
