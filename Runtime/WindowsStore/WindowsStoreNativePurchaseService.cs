#if UNITY_WSA && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Windows.Services.Store;

namespace UnityStoreKit
{
    public sealed class WindowsStoreNativePurchaseService : IPurchaseService
    {
        public bool IsInitialized { get; private set; }

        public event Action<string> OnPurchaseSucceeded;
        public event Action<string, string> OnPurchaseFailed;
        public event Action<string> OnPurchaseRestored;
        public event Action<string, string> OnPurchasePendingOrUnknown;
        public event Action OnProductsLoaded;

        private IAPCatalog catalog;
        private List<ProductDefinition> productDefinitions = new();
        private readonly Dictionary<string, string> internalToStoreIdMap = new();
        private readonly Dictionary<string, string> storeToInternalIdMap = new();
        private StoreContext storeContext;
        private StoreAppLicense appLicense;

        private readonly Dictionary<string, bool> ownedProducts = new();
        private readonly List<WindowsStoreProductInfo> storeProductInfos = new();

        /// <summary>
        /// 使用 ScriptableObject 资产配置初始化微软原生商店服务。
        /// </summary>
        public void Initialize(IAPCatalog catalog)
        {
            this.catalog = catalog;
            Initialize(catalog != null ? catalog.products : null);
        }

        /// <summary>
        /// 使用动态产品列表配置初始化服务（解耦 ScriptableObject 资产文件）。
        /// </summary>
        public async void Initialize(List<ProductDefinition> products)
        {
            this.productDefinitions = products ?? new List<ProductDefinition>();
            internalToStoreIdMap.Clear();
            storeToInternalIdMap.Clear();

            foreach (var p in productDefinitions)
            {
                internalToStoreIdMap[p.internalId] = p.storeId;
                storeToInternalIdMap[p.storeId] = p.internalId;
            }

            Debug.Log("[WindowsStore] Native initialization starting...");
            IsInitialized = false;

            try
            {
                // 获取当前应用启动上下文的默认微软 StoreContext 实例
                storeContext = StoreContext.GetDefault();
                if (storeContext == null)
                {
                    Debug.LogError("[WindowsStore] Failed to acquire default StoreContext.");
                    IsInitialized = false;
                    return;
                }

                // 异步获取应用授权证书
                Debug.Log("[WindowsStore] Requesting App License...");
                var license = await storeContext.GetAppLicenseAsync();

                // 异步加载商品目录元数据
                string[] productKinds = { "Durable", "Consumable", "UnmanagedConsumable" };
                List<string> filterList = new List<string>(productKinds);
                Debug.Log("[WindowsStore] Fetching associated products metadata from Microsoft Store...");
                var queryResult = await storeContext.GetAssociatedStoreProductsAsync(filterList);

                // 在主线程更新所有商品目录状态并刷新拥有商品列表
                RunOnAppThread(() =>
                {
                    appLicense = license;
                    storeProductInfos.Clear();

                    if (queryResult != null && queryResult.ExtendedError == null)
                    {
                        Debug.Log($"[WindowsStore] Found {queryResult.Products.Count} associated products in Store catalog.");
                        foreach (var pair in queryResult.Products)
                        {
                            var storeProduct = pair.Value;
                            if (storeProduct == null) continue;

                            string internalId = ToInternalProductId(storeProduct.StoreId);
                            var info = new WindowsStoreProductInfo
                            {
                                InternalProductId = internalId,
                                WindowsStoreProductId = storeProduct.StoreId,
                                Title = storeProduct.Title,
                                Description = storeProduct.Description,
                                FormattedPrice = storeProduct.Price?.FormattedPrice ?? "N/A",
                                IsOwned = false,
                                ProductKind = storeProduct.ProductKind
                            };

                            storeProductInfos.Add(info);
                            Debug.Log($"[WindowsStore] Catalog loaded: {info.InternalProductId} -> Title: {info.Title}, Price: {info.FormattedPrice}, Owned: {info.IsOwned}");
                        }
                    }
                    else if (queryResult?.ExtendedError != null)
                    {
                        Debug.LogError($"[WindowsStore] Catalog query failed: {queryResult.ExtendedError.Message}");
                    }

                    // 刷新拥有列表
                    RefreshOwnedLicenses(appLicense);

                    // 触发商品目录加载完毕事件
                    OnProductsLoaded?.Invoke();
                });

                // 异步核销和补发应用商店中未正常履行的消耗型漏单商品
                await RecoverUnconsumedConsumablesAsync();

                RunOnAppThread(() =>
                {
                    IsInitialized = true;
                    Debug.Log("[WindowsStore] Native Store successfully initialized.");
                });
            }
            catch (Exception ex)
            {
                RunOnAppThread(() =>
                {
                    IsInitialized = false;
                    Debug.LogError($"[WindowsStore] Native initialization encountered an exception: {ex}");
                });
            }
        }

        /// <summary>
        /// 发起对应商品ID的微软商店内购支付流程。
        /// </summary>
        /// <param name="productId">游戏内通用商品唯一标识ID。</param>
        public async void Buy(string productId)
        {
            if (!IsInitialized || storeContext == null)
            {
                Debug.LogError($"[WindowsStore] Cannot purchase {productId}. Store is not initialized.");
                OnPurchaseFailed?.Invoke(productId, "Store is not initialized.");
                return;
            }

            // 将游戏内通用商品 ID 转换为微软商店后台配置的 12 位 alphanumeric 唯一产品 ID
            string windowsStoreProductId = ToWindowsStoreId(productId);
            Debug.Log($"[WindowsStore] Initiating RequestPurchaseAsync for: {windowsStoreProductId} (Internal: {productId})");

            try
            {
                // 唤起微软官方原生内购结账支付流程
                var result = await storeContext.RequestPurchaseAsync(windowsStoreProductId);
                
                if (result == null)
                {
                    RunOnAppThread(() =>
                    {
                        Debug.LogError($"[WindowsStore] RequestPurchaseAsync returned null for product {windowsStoreProductId}.");
                        OnPurchaseFailed?.Invoke(productId, "Purchase request returned no response.");
                    });
                    return;
                }

                Debug.Log($"[WindowsStore] RequestPurchaseAsync response status: {result.Status} for product: {productId}");

                switch (result.Status)
                {
                    case StorePurchaseStatus.Succeeded:
                        await HandleConsumableFulfillmentIfNeeded(productId, windowsStoreProductId);
                        RunOnAppThread(() => OnPurchaseSucceeded?.Invoke(productId));
                        break;

                    case StorePurchaseStatus.AlreadyPurchased:
                        await HandleConsumableFulfillmentIfNeeded(productId, windowsStoreProductId);
                        RunOnAppThread(() =>
                        {
                            Debug.Log($"[WindowsStore] Product {productId} is already owned. Resolving as success.");
                            OnPurchaseSucceeded?.Invoke(productId);
                        });
                        break;

                    case StorePurchaseStatus.NotPurchased:
                        RunOnAppThread(() => OnPurchaseFailed?.Invoke(productId, "User canceled or did not complete purchase."));
                        break;

                    case StorePurchaseStatus.NetworkError:
                        RunOnAppThread(() => OnPurchaseFailed?.Invoke(productId, "Purchase failed due to network error."));
                        break;

                    case StorePurchaseStatus.ServerError:
                        RunOnAppThread(() => OnPurchaseFailed?.Invoke(productId, "Purchase failed due to Microsoft Store server error."));
                        break;

                    default:
                        RunOnAppThread(() => OnPurchasePendingOrUnknown?.Invoke(productId, result.Status.ToString()));
                        break;
                }

                if (result.ExtendedError != null)
                {
                    Debug.LogWarning($"[WindowsStore] Extended HRESULT error: {result.ExtendedError.Message}");
                }
            }
            catch (Exception ex)
            {
                RunOnAppThread(() =>
                {
                    Debug.LogError($"[WindowsStore] Purchase exception occurred: {ex}");
                    OnPurchaseFailed?.Invoke(productId, $"Purchase exception: {ex.Message}");
                });
            }
        }

        /// <summary>
        /// 恢复玩家的历史购买订单（如去广告等非消费性永久买断商品或有效订阅项）。
        /// </summary>
        public async void RestorePurchases()
        {
            if (storeContext == null)
            {
                Debug.LogWarning("[WindowsStore] Restore failed: StoreContext is uninitialized.");
                return;
            }

            Debug.Log("[WindowsStore] Native RestorePurchases start. Fetching fresh license...");

            try
            {
                // 重新异步向微软服务器请求获取最新的应用程序授权证书
                var license = await storeContext.GetAppLicenseAsync();
                
                RunOnAppThread(() =>
                {
                    appLicense = license;
                    RefreshOwnedLicenses(appLicense);

                    int restoredCount = 0;
                    foreach (var pair in ownedProducts)
                    {
                        if (pair.Value)
                        {
                            // 消耗型商品不需要恢复
                            if (IsProductConsumable(pair.Key))
                            {
                                continue;
                            }

                            Debug.Log($"[WindowsStore] Restored native ownership for: {pair.Key}");
                            OnPurchaseRestored?.Invoke(pair.Key);
                            restoredCount++;
                        }
                    }
                    Debug.Log($"[WindowsStore] Native RestorePurchases complete. Restored {restoredCount} items.");
                });
            }
            catch (Exception ex)
            {
                RunOnAppThread(() =>
                {
                    Debug.LogError($"[WindowsStore] Native restore exception: {ex}");
                });
            }
        }

        public bool IsPurchased(string productId)
        {
            return ownedProducts.TryGetValue(productId, out bool owned) && owned;
        }

        public List<WindowsStoreProductInfo> GetProductInfos()
        {
            return storeProductInfos;
        }

        private void RunOnAppThread(Action action)
        {
            // 将操作调度至 Unity 主线程（App 线程）上异步执行
            UnityEngine.WSA.Application.InvokeOnAppThread(() => action(), false);
        }

        #region Helper Methods
        /// <summary>
        /// 异步查询微软后台未核销的消耗品，并执行核销与道具补发，避免漏单。
        /// </summary>
        private async Task RecoverUnconsumedConsumablesAsync()
        {
            if (storeContext == null) return;
            Debug.Log("[WindowsStore] Checking for unconsumed consumables on Microsoft Store...");
            try
            {
                string[] productKinds = { "Consumable", "UnmanagedConsumable" };
                List<string> filterList = new List<string>(productKinds);
                
                // 获取用户已购买但未核销 of 商品集合
                var queryResult = await storeContext.GetUserCollectionAsync(filterList);

                if (queryResult == null)
                {
                    Debug.LogWarning("[WindowsStore] GetUserCollectionAsync returned null result.");
                    return;
                }

                if (queryResult.ExtendedError != null)
                {
                    Debug.LogError($"[WindowsStore] Unconsumed consumables query failed: {queryResult.ExtendedError.Message}");
                    return;
                }

                Debug.Log($"[WindowsStore] Found {queryResult.Products.Count} potential unconsumed consumables in User Collection.");

                foreach (var pair in queryResult.Products)
                {
                    var storeProduct = pair.Value;
                    if (storeProduct == null) continue;

                    string storeId = storeProduct.StoreId;
                    string internalProductId = ToInternalProductId(storeId);
                    Guid trackingId = Guid.NewGuid();

                    uint quantityToFulfill = 0;

                    if (storeProduct.ProductKind == "UnmanagedConsumable")
                    {
                        // 开发者托管 of 消耗品，未核销时默认数量为 1
                        quantityToFulfill = 1;
                    }
                    else if (storeProduct.ProductKind == "Consumable")
                    {
                        // 微软托管 of 消耗品，查询其余额
                        var balanceResult = await storeContext.GetConsumableBalanceRemainingAsync(storeId);
                        if (balanceResult != null && balanceResult.ExtendedError == null && balanceResult.Status == StoreConsumableStatus.Succeeded)
                        {
                            quantityToFulfill = balanceResult.BalanceRemaining;
                        }
                    }

                    if (quantityToFulfill > 0)
                    {
                        Debug.Log($"[WindowsStore] Recovering unconsumed consumable: {storeId} (Internal: {internalProductId}), quantity: {quantityToFulfill}, trackingId: {trackingId}");

                        // 执行消耗上报（Fulfillment）
                        var fulfillResult = await storeContext.ReportConsumableFulfillmentAsync(storeId, quantityToFulfill, trackingId);
                        if (fulfillResult != null)
                        {
                            Debug.Log($"[WindowsStore] Recovery fulfillment status for {internalProductId}: {fulfillResult.Status}");
                            if (fulfillResult.Status == StoreConsumableStatus.Succeeded)
                            {
                                RunOnAppThread(() =>
                                {
                                    ownedProducts[internalProductId] = false;
                                    UpdateProductOwnership(internalProductId, false);

                                    // 触发购买成功回调，让 PurchaseManager 给用户补发漏掉 of 道具或金币
                                    OnPurchaseSucceeded?.Invoke(internalProductId);
                                });
                            }
                            else
                            {
                                Debug.LogError($"[WindowsStore] Recovery fulfillment failed for {internalProductId}. Status: {fulfillResult.Status}, Error: {fulfillResult.ExtendedError?.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WindowsStore] Exception while recovering unconsumed consumables: {ex}");
            }
        }

        /// <summary>
        /// 根据最新的应用授权证书，解析并刷新本地记录的已拥有的永久商品或订阅授权列表。
        /// </summary>
        private void RefreshOwnedLicenses(StoreAppLicense license)
        {
            ownedProducts.Clear();

            // Reset all products to unowned first to ensure stale/expired licenses are cleared
            foreach (var info in storeProductInfos)
            {
                info.IsOwned = false;
                info.ExpirationDate = DateTime.MinValue;
            }

            if (license == null)
            {
                Debug.LogWarning("[WindowsStore] App license reference is null.");
                return;
            }

            Debug.Log("[WindowsStore] Active Add-on licenses count: " + license.AddOnLicenses.Count);

            foreach (var addOnLicense in license.AddOnLicenses)
            {
                string windowsStoreProductId = addOnLicense.Key;
                var storeLicense = addOnLicense.Value;

                string internalProductId = ToInternalProductId(windowsStoreProductId);
                bool isActive = storeLicense != null && storeLicense.IsActive;

                ownedProducts[internalProductId] = isActive;
                DateTime expiration = storeLicense != null ? storeLicense.ExpirationDate.DateTime : DateTime.MinValue;
                UpdateProductOwnership(internalProductId, isActive, expiration);

                Debug.Log($"[WindowsStore] License - AddOn: {windowsStoreProductId} (Internal: {internalProductId}), Active: {isActive}");
            }
        }

        // 已弃用：旧的商品元数据加载方法已整合进主线程调度的 Initialize 流程中

        private void UpdateProductOwnership(string productId, bool isOwned, DateTime expirationDate = default)
        {
            var info = storeProductInfos.Find(p => p.InternalProductId == productId);
            if (info != null)
            {
                info.IsOwned = isOwned;
                info.ExpirationDate = expirationDate;
            }
        }

        /// <summary>
        /// 在内购交易成功时，针对开发人员管理的消耗型商品（Consumable）执行微软特有的消耗上报（Fulfillment）。
        /// 确认上报完成后，玩家才可再次购买该特定商品。
        /// </summary>
        private async Task HandleConsumableFulfillmentIfNeeded(string productId, string windowsStoreProductId)
        {
            bool isConsumable = IsProductConsumable(productId);

            if (isConsumable)
            {
                Debug.Log($"[WindowsStore] Product {productId} is a Developer-managed consumable. Initiating fulfillment...");
                Guid trackingId = Guid.NewGuid();
                try
                {
                    // 传入数量参数 1U 以及跟踪标识符 trackingId，以正确匹配 Windows SDK 的 StoreContext.ReportConsumableFulfillmentAsync 签名
                    var fulfillResult = await storeContext.ReportConsumableFulfillmentAsync(windowsStoreProductId, 1U, trackingId);
                    RunOnAppThread(() =>
                    {
                        if (fulfillResult != null)
                        {
                            Debug.Log($"[WindowsStore] Fulfillment status for {productId}: {fulfillResult.Status}");
                            if (fulfillResult.Status == StoreConsumableStatus.Succeeded)
                            {
                                ownedProducts[productId] = false;
                                UpdateProductOwnership(productId, false);
                            }
                            else
                            {
                                Debug.LogError($"[WindowsStore] Fulfillment failed for {productId}. Status: {fulfillResult.Status}, Error: {fulfillResult.ExtendedError?.Message}");
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    RunOnAppThread(() =>
                    {
                        Debug.LogError($"[WindowsStore] Exception reporting fulfillment for {productId}: {ex}");
                    });
                }
            }
            else
            {
                // Non-consumable (durable) or Store-managed
                RunOnAppThread(() =>
                {
                    ownedProducts[productId] = true;
                    UpdateProductOwnership(productId, true);
                });
            }
        }


        /// <summary>
        /// 判断指定的产品唯一标识符是否为消费型商品（Consumable）。
        /// </summary>
        private bool IsProductConsumable(string productId)
        {
            var info = storeProductInfos.Find(p => p.InternalProductId == productId);
            if (info != null)
            {
                return info.ProductKind == "Consumable" || info.ProductKind == "UnmanagedConsumable";
            }
            // 后备方案：从 列表/Catalog 判定
            var def = productDefinitions.Find(p => p.internalId == productId);
            if (def != null)
            {
                return def.productType == ProductType.Consumable;
            }
            // 兜底方案：从 IAPCatalog 判定
            var defCatalog = catalog != null ? catalog.GetByInternalId(productId) : null;
            if (defCatalog != null)
            {
                return defCatalog.productType == ProductType.Consumable;
            }
            return false;
        }

        private string ToWindowsStoreId(string internalProductId)
        {
            if (internalToStoreIdMap.TryGetValue(internalProductId, out string storeId))
            {
                return storeId;
            }
            return internalProductId;
        }

        private string ToInternalProductId(string windowsStoreProductId)
        {
            if (storeToInternalIdMap.TryGetValue(windowsStoreProductId, out string internalId))
            {
                return internalId;
            }
            return windowsStoreProductId;
        }
        #endregion
    }
}
#endif
