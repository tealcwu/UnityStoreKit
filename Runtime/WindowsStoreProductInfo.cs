namespace UnityStoreKit
{
    [System.Serializable]
    public sealed class WindowsStoreProductInfo
    {
        /// <summary>
        /// The internal product ID used by game logic (e.g. "remove_ads").
        /// </summary>
        public string InternalProductId;

        /// <summary>
        /// The product ID configured in the Microsoft Partner Center.
        /// </summary>
        public string WindowsStoreProductId;

        /// <summary>
        /// Localized product title.
        /// </summary>
        public string Title;

        /// <summary>
        /// Localized product description.
        /// </summary>
        public string Description;

        /// <summary>
        /// Localized, formatted price (e.g., "$0.99", "￥6.00").
        /// </summary>
        public string FormattedPrice;

        /// <summary>
        /// Whether the product is currently owned by the user.
        /// </summary>
        public bool IsOwned;

        /// <summary>
        /// The native Microsoft Store product kind (e.g. "Durable", "Consumable", "UnmanagedConsumable").
        /// </summary>
        public string ProductKind;

        /// <summary>
        /// The expiration date of the product if it is a subscription.
        /// </summary>
        public System.DateTime ExpirationDate;
    }
}
