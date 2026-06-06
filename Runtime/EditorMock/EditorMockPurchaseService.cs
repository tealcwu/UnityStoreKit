using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace UnityStoreKit
{
    public sealed class EditorMockPurchaseService : IPurchaseService
    {
        public bool IsInitialized { get; private set; }

        public event Action<string> OnPurchaseSucceeded;
        public event Action<string, string> OnPurchaseFailed;
        public event Action<string> OnPurchaseRestored;
#pragma warning disable 0067
        public event Action<string, string> OnPurchasePendingOrUnknown;
#pragma warning restore 0067
        public event Action OnProductsLoaded;

        private IAPCatalog catalog;
        private List<ProductDefinition> productDefinitions = new();
        private readonly Dictionary<string, bool> ownedProducts = new();
        private readonly List<WindowsStoreProductInfo> mockProducts = new();

        private bool isMockStoreSuppressed = false;

        private const string PrefsPrefix = "MockIAP_Owned_";

        // Simple setting to simulate failure (can be set from editor tester script if needed)
        public static bool ForceFailPurchases = false;
        public static string CustomFailureReason = "Simulated network failure in Unity Editor.";

        /// <summary>
        /// 使用 ScriptableObject 资产初始化模拟商店服务。
        /// </summary>
        public void Initialize(IAPCatalog catalog)
        {
            this.catalog = catalog;
            Initialize(catalog != null ? catalog.products : null);
        }

        /// <summary>
        /// 使用动态产品列表配置初始化模拟商店服务（方法 A，解耦资产配置）。
        /// </summary>
        public async void Initialize(List<ProductDefinition> products)
        {
            this.productDefinitions = products ?? new List<ProductDefinition>();
            Debug.Log("[WindowsStore][Mock] Initialize start.");
            IsInitialized = false;
            isMockStoreSuppressed = false;

            // Simulate network delay
            await Task.Delay(1000);

            // Setup mock product catalog
            mockProducts.Clear();
            foreach (var p in productDefinitions)
            {
                AddMockProduct(p.internalId, p.storeId, p.mockTitle, p.mockDescription, p.mockPrice, p.productType.ToString());
            }

            // Refresh loaded licenses from PlayerPrefs
            LoadPersistentLicenses();

            IsInitialized = true;
            Debug.Log("[WindowsStore][Mock] Initialized successfully. Licenses loaded.");

            OnProductsLoaded?.Invoke();
        }

        public async void Buy(string productId)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[WindowsStore][Mock] Cannot buy. Service is not initialized.");
                OnPurchaseFailed?.Invoke(productId, "Store service not initialized.");
                return;
            }

            Debug.Log($"[WindowsStore][Mock] Buy request started for: {productId}");
            
            bool confirmed = true;
#if UNITY_EDITOR
            var prod = mockProducts.Find(p => p.InternalProductId == productId);
            string title = prod != null ? prod.Title : productId;
            string price = prod != null ? prod.FormattedPrice : "N/A";
            confirmed = UnityEditor.EditorUtility.DisplayDialog(
                "Mock Microsoft Store - Checkout",
                $"Product: {title}\nPrice: {price}\n\nDo you want to authorize this purchase?",
                "Buy",
                "Cancel"
            );
#endif

            if (!confirmed)
            {
                Debug.LogWarning($"[WindowsStore][Mock] Purchase canceled by user: {productId}");
                OnPurchaseFailed?.Invoke(productId, "User canceled or did not complete purchase.");
                return;
            }

            // Simulate network roundtrip
            await Task.Delay(800);

            if (ForceFailPurchases)
            {
                Debug.LogWarning($"[WindowsStore][Mock] Purchase failed (Forced): {productId}");
                OnPurchaseFailed?.Invoke(productId, CustomFailureReason);
                return;
            }

            var def = productDefinitions.Find(p => p.internalId == productId);
            if (def == null && catalog != null)
            {
                def = catalog.GetByInternalId(productId);
            }
            bool isConsumable = def != null && def.productType == ProductType.Consumable;

            // Successfully purchased / already owned
            isMockStoreSuppressed = false;
            ownedProducts[productId] = !isConsumable;
            SavePersistentLicense(productId, !isConsumable);

            // Update in mock products
            var prodInfo = mockProducts.Find(p => p.InternalProductId == productId);
            if (prodInfo != null)
            {
                prodInfo.IsOwned = !isConsumable;
                if (def != null && def.productType == ProductType.Subscription)
                {
                    var expiration = DateTime.Now.AddDays(30);
                    PlayerPrefs.SetString("MockIAP_SubExpiration_" + productId, expiration.ToString("o"));
                    PlayerPrefs.Save();
                    prodInfo.ExpirationDate = expiration;
                }
            }

            Debug.Log($"[WindowsStore][Mock] Purchase succeeded for: {productId}");
            OnPurchaseSucceeded?.Invoke(productId);
        }

        public async void RestorePurchases()
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[WindowsStore][Mock] Cannot restore. Service is not initialized.");
                return;
            }

            Debug.Log("[WindowsStore][Mock] RestorePurchases start.");
            await Task.Delay(500);

            // Reactivate mock licenses
            isMockStoreSuppressed = false;

            LoadPersistentLicenses();

            int restoredCount = 0;
            foreach (var pair in ownedProducts)
            {
                if (pair.Value)
                {
                    var def = productDefinitions.Find(p => p.internalId == pair.Key);
                    if (def == null && catalog != null)
                    {
                        def = catalog.GetByInternalId(pair.Key);
                    }
                    if (def != null && def.productType == ProductType.Consumable)
                    {
                        continue;
                    }

                    Debug.Log($"[WindowsStore][Mock] Restored product: {pair.Key}");
                    OnPurchaseRestored?.Invoke(pair.Key);
                    restoredCount++;
                }
            }

            Debug.Log($"[WindowsStore][Mock] RestorePurchases complete. Restored {restoredCount} products.");
        }

        public bool IsPurchased(string productId)
        {
            if (isMockStoreSuppressed) return false;
            return ownedProducts.TryGetValue(productId, out bool owned) && owned;
        }

        public List<WindowsStoreProductInfo> GetProductInfos()
        {
            if (isMockStoreSuppressed)
            {
                // Return a temporarily suppressed copy for UI queries
                var suppressedList = new List<WindowsStoreProductInfo>();
                foreach (var prod in mockProducts)
                {
                    suppressedList.Add(new WindowsStoreProductInfo
                    {
                        InternalProductId = prod.InternalProductId,
                        WindowsStoreProductId = prod.WindowsStoreProductId,
                        Title = prod.Title,
                        Description = prod.Description,
                        FormattedPrice = prod.FormattedPrice,
                        IsOwned = false,
                        ProductKind = prod.ProductKind,
                        ExpirationDate = DateTime.MinValue
                    });
                }
                return suppressedList;
            }
            return mockProducts;
        }

        /// <summary>
        /// Temporarily suppresses mock active licenses to simulate game local cache clear.
        /// </summary>
        public void SuppressMockLicenses()
        {
            isMockStoreSuppressed = true;
            Debug.Log("[WindowsStore][Mock] Active licenses have been temporarily suppressed to simulate local cache clear.");
        }

        // Helper to populate mock database
        private void AddMockProduct(string internalId, string storeId, string title, string desc, string price, string productKind)
        {
            bool owned = PlayerPrefs.GetInt(PrefsPrefix + internalId, 0) == 1;

            mockProducts.Add(new WindowsStoreProductInfo
            {
                InternalProductId = internalId,
                WindowsStoreProductId = storeId,
                Title = title,
                Description = desc,
                FormattedPrice = price,
                IsOwned = owned,
                ProductKind = productKind
            });

            ownedProducts[internalId] = owned;
        }

        private void LoadPersistentLicenses()
        {
            // Sync current owned statuses from persistent store
            foreach (var prod in mockProducts)
            {
                bool owned = PlayerPrefs.GetInt(PrefsPrefix + prod.InternalProductId, 0) == 1;

                var def = productDefinitions.Find(p => p.internalId == prod.InternalProductId);
                if (def == null && catalog != null)
                {
                    def = catalog.GetByInternalId(prod.InternalProductId);
                }

                if (def != null && def.productType == ProductType.Consumable)
                {
                    // Consumables are never persistently owned
                    owned = false;
                    SavePersistentLicense(prod.InternalProductId, false);
                }
                else if (def != null && def.productType == ProductType.Subscription)
                {
                    string expStr = PlayerPrefs.GetString("MockIAP_SubExpiration_" + prod.InternalProductId, "");
                    if (DateTime.TryParse(expStr, out DateTime expiration))
                    {
                        prod.ExpirationDate = expiration;
                        // Check if it's expired
                        if (DateTime.Now > expiration)
                        {
                            owned = false;
                            ownedProducts[prod.InternalProductId] = false;
                            SavePersistentLicense(prod.InternalProductId, false);
                        }
                    }
                }

                ownedProducts[prod.InternalProductId] = owned;
                prod.IsOwned = owned;
                if (owned)
                {
                    Debug.Log($"[WindowsStore][Mock] Persistent license active: {prod.InternalProductId}");
                }
            }
        }

        private void SavePersistentLicense(string productId, bool owned)
        {
            PlayerPrefs.SetInt(PrefsPrefix + productId, owned ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Test utility to clear all mock persistent purchases.
        /// </summary>
        public void ClearAllMockPurchases()
        {
            foreach (var prod in mockProducts)
            {
                PlayerPrefs.DeleteKey(PrefsPrefix + prod.InternalProductId);
                
                var def = productDefinitions.Find(p => p.internalId == prod.InternalProductId);
                if (def == null && catalog != null)
                {
                    def = catalog.GetByInternalId(prod.InternalProductId);
                }

                if (def != null && def.productType == ProductType.Subscription)
                {
                    PlayerPrefs.DeleteKey("MockIAP_SubExpiration_" + prod.InternalProductId);
                }

                ownedProducts[prod.InternalProductId] = false;
                prod.IsOwned = false;
                prod.ExpirationDate = DateTime.MinValue;
            }
            PlayerPrefs.Save();
            Debug.Log("[WindowsStore][Mock] All mock purchases cleared.");
        }
    }
}
