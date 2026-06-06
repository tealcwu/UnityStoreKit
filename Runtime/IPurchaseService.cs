using System;
using System.Collections.Generic;

namespace UnityStoreKit
{
    public interface IPurchaseService
    {
        /// <summary>
        /// Indicates whether the purchase service has been successfully initialized.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Triggered when a purchase completes successfully. Passes the internal Product ID.
        /// </summary>
        event Action<string> OnPurchaseSucceeded;

        /// <summary>
        /// Triggered when a purchase fails. Passes the internal Product ID and the failure reason.
        /// </summary>
        event Action<string, string> OnPurchaseFailed;

        /// <summary>
        /// Triggered when a purchase is successfully restored. Passes the internal Product ID.
        /// </summary>
        event Action<string> OnPurchaseRestored;

        /// <summary>
        /// Triggered when a purchase is in a pending or unknown state. Passes the internal Product ID and status string.
        /// </summary>
        event Action<string, string> OnPurchasePendingOrUnknown;

        /// <summary>
        /// Triggered when the store products metadata are successfully loaded.
        /// </summary>
        event Action OnProductsLoaded;

        /// <summary>
        /// Initializes the purchase service asynchronously with the product catalog.
        /// </summary>
        /// <param name="catalog">The configuration catalog of products.</param>
        void Initialize(IAPCatalog catalog);

        /// <summary>
        /// 在不依赖 IAPCatalog 资产时，直接传入产品定义列表进行初始化。
        /// </summary>
        /// <param name="products">产品定义配置列表。</param>
        void Initialize(List<ProductDefinition> products);

        /// <summary>
        /// Initiates a purchase flow for the specified product.
        /// </summary>
        /// <param name="productId">The internal product ID defined in PurchaseProductIds.</param>
        void Buy(string productId);

        /// <summary>
        /// Queries the store for previously purchased active licenses and restores them.
        /// </summary>
        void RestorePurchases();

        /// <summary>
        /// Queries whether the user currently owns the specified product.
        /// </summary>
        /// <param name="productId">The internal product ID.</param>
        /// <returns>True if owned, otherwise false.</returns>
        bool IsPurchased(string productId);

        /// <summary>
        /// Returns the list of queried store product information (localised titles, prices, etc.).
        /// </summary>
        List<WindowsStoreProductInfo> GetProductInfos();
    }
}
