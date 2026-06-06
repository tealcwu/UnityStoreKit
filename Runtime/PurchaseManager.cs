using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityStoreKit
{
    [DisallowMultipleComponent]
    public sealed class PurchaseManager : MonoBehaviour
    {
        public static PurchaseManager Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("The configuration catalog of products.")]
        [SerializeField] private IAPCatalog catalog;

        [Tooltip("If enabled, the purchase service will initialize automatically on Awake.")]
        [SerializeField] private bool initializeOnAwake = true;

        private IPurchaseService purchaseService;
        private bool isProcessingPurchase;

        /// <summary>
        /// Indicates if the underlying store service is ready.
        /// </summary>
        public bool IsInitialized => purchaseService != null && purchaseService.IsInitialized;

        /// <summary>
        /// Gets the underlying active purchase service. Useful for platform-specific debugging.
        /// </summary>
        public IPurchaseService ActiveService => purchaseService;

        #region Public Events
        public event Action<string> OnPurchaseSucceeded;
        public event Action<string, string> OnPurchaseFailed;
        public event Action<string> OnPurchaseRestored;
        public event Action<string, string> OnPurchasePendingOrUnknown;
        public event Action OnProductsLoaded;
        #endregion

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeService();
        }

        private void InitializeService()
        {
            Debug.Log("[WindowsStore][Manager] Initializing purchase service for current platform...");

#if UNITY_WSA && !UNITY_EDITOR
            purchaseService = new WindowsStoreNativePurchaseService();
#else
            // In Editor or non-WSA platforms, we fall back to the Mock Service.
            purchaseService = new EditorMockPurchaseService();
#endif

            // Wire up callbacks
            purchaseService.OnPurchaseSucceeded += HandlePurchaseSucceeded;
            purchaseService.OnPurchaseFailed += HandlePurchaseFailed;
            purchaseService.OnPurchaseRestored += HandlePurchaseRestored;
            purchaseService.OnPurchasePendingOrUnknown += HandlePurchasePendingOrUnknown;
            purchaseService.OnProductsLoaded += HandleProductsLoaded;

            if (initializeOnAwake && catalog != null)
            {
                purchaseService.Initialize(catalog);
            }
        }

        /// <summary>
        /// 手动触发购买服务的初始化（配合已挂载的 IAPCatalog 资产配置）。
        /// </summary>
        public void Initialize()
        {
            if (purchaseService != null && !purchaseService.IsInitialized && catalog != null)
            {
                purchaseService.Initialize(catalog);
            }
        }

        /// <summary>
        /// 动态传入产品列表配置并进行手动初始化（方法 A，解耦资产文件配置）。
        /// </summary>
        /// <param name="products">代码定义的商品注册列表。</param>
        public void Initialize(List<ProductDefinition> products)
        {
            if (purchaseService != null && !purchaseService.IsInitialized && products != null)
            {
                purchaseService.Initialize(products);
            }
        }

        /// <summary>
        /// Initiates the purchase of a product.
        /// </summary>
        public void Buy(string productId)
        {
            if (purchaseService == null)
            {
                Debug.LogError("[WindowsStore][Manager] Purchase failed: Purchase service is not initialized.");
                OnPurchaseFailed?.Invoke(productId, "Purchase service unavailable.");
                return;
            }

            if (isProcessingPurchase)
            {
                Debug.LogWarning($"[WindowsStore][Manager] Another purchase is currently in progress. Ignoring request for: {productId}");
                return;
            }

            isProcessingPurchase = true;
            Debug.Log($"[WindowsStore][Manager] Initiating purchase for: {productId}");
            purchaseService.Buy(productId);
        }

        /// <summary>
        /// Triggers the restoration of owned licenses.
        /// </summary>
        public void RestorePurchases()
        {
            if (purchaseService == null)
            {
                Debug.LogError("[WindowsStore][Manager] Restore failed: Purchase service is not initialized.");
                return;
            }

            Debug.Log("[WindowsStore][Manager] Requesting purchase restoration.");
            purchaseService.RestorePurchases();
        }

        /// <summary>
        /// Queries if a specific product is currently owned by the user.
        /// </summary>
        public bool IsPurchased(string productId)
        {
            if (purchaseService == null) return false;
            return purchaseService.IsPurchased(productId);
        }

        /// <summary>
        /// Returns all metadata retrieved for the products in the catalog.
        /// </summary>
        public List<WindowsStoreProductInfo> GetProductInfos()
        {
            if (purchaseService == null) return new List<WindowsStoreProductInfo>();
            return purchaseService.GetProductInfos();
        }

        #region Internal Event Handlers
        private void HandlePurchaseSucceeded(string productId)
        {
            isProcessingPurchase = false;
            Debug.Log($"[WindowsStore][Manager] Event: Purchase succeeded for {productId}");
            OnPurchaseSucceeded?.Invoke(productId);
        }

        private void HandlePurchaseFailed(string productId, string reason)
        {
            isProcessingPurchase = false;
            Debug.LogWarning($"[WindowsStore][Manager] Event: Purchase failed for {productId}. Reason: {reason}");
            OnPurchaseFailed?.Invoke(productId, reason);
        }

        private void HandlePurchaseRestored(string productId)
        {
            Debug.Log($"[WindowsStore][Manager] Event: Purchase restored for {productId}");
            OnPurchaseRestored?.Invoke(productId);
        }

        private void HandlePurchasePendingOrUnknown(string productId, string status)
        {
            isProcessingPurchase = false;
            Debug.LogWarning($"[WindowsStore][Manager] Event: Purchase pending or unknown for {productId}. Status: {status}");
            OnPurchasePendingOrUnknown?.Invoke(productId, status);
        }

        private void HandleProductsLoaded()
        {
            Debug.Log("[WindowsStore][Manager] Event: Product metadata loaded.");
            OnProductsLoaded?.Invoke();
        }
        #endregion

        #region Helper Game Logic Persistence
        public (bool isActive, DateTime expiration) GetSubscriptionStatus(string productId)
        {
            if (purchaseService == null) return (false, DateTime.MinValue);

            var infos = purchaseService.GetProductInfos();
            var info = infos.Find(p => p.InternalProductId == productId);

            if (info != null)
            {
                return (info.IsOwned, info.ExpirationDate);
            }

            return (false, DateTime.MinValue);
        }
        #endregion
    }
}
