namespace ECommerceSite.Helpers
{
    /// <summary>
    /// Customers order as guests (no login required), so the cart is tracked
    /// using a long-lived identifier stored in a cookie instead of a UserId.
    /// </summary>
    public static class CartCookie
    {
        public const string CookieName = "PorkCartId";

        public static string GetOrCreateCartId(HttpContext httpContext)
        {
            if (httpContext.Request.Cookies.TryGetValue(CookieName, out var cartId) && !string.IsNullOrWhiteSpace(cartId))
            {
                return cartId;
            }

            cartId = Guid.NewGuid().ToString("N");

            httpContext.Response.Cookies.Append(CookieName, cartId, new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            });

            return cartId;
        }
    }
}
