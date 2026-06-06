using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UnityStoreKit
{
    public sealed class IAPTestUIController : MonoBehaviour
    {
        [Header("UI Reference - Buttons")]
        [SerializeField] private Button btnInitialize;
        [SerializeField] private Button btnBuyRemoveAds;
        [SerializeField] private Button btnBuyCoins100;
        [SerializeField] private Button btnBuyCoins500;
        [SerializeField] private Button btnBuyCoins1000;
        [SerializeField] private Button btnBuyMonthSub;
        [SerializeField] private Button btnRestorePurchases;
        [SerializeField] private Button btnClearCache;
        [SerializeField] private Button btnToggleFail;

        [Header("UI Reference - Texts")]
        [SerializeField] private TextMeshProUGUI txtInitStatus;
        [SerializeField] private TextMeshProUGUI txtAdsStatus;
        [SerializeField] private TextMeshProUGUI txtFailStatus;
        [SerializeField] private TextMeshProUGUI txtCoinsBalance;
        [SerializeField] private TextMeshProUGUI txtSubscriptionStatus;
        [SerializeField] private TextMeshProUGUI txtLogs;

        private readonly StringBuilder logBuilder = new();
        private ScrollRect scrollRect;

        private void Start()
        {
            // Subscribe to PurchaseManager events
            if (PurchaseManager.Instance != null)
            {
                PurchaseManager.Instance.OnPurchaseSucceeded += OnPurchaseSucceeded;
                PurchaseManager.Instance.OnPurchaseFailed += OnPurchaseFailed;
                PurchaseManager.Instance.OnPurchaseRestored += OnPurchaseRestored;
                PurchaseManager.Instance.OnPurchasePendingOrUnknown += OnPurchasePendingOrUnknown;
                PurchaseManager.Instance.OnProductsLoaded += OnProductsLoaded;
            }

            // Bind buttons
            if (btnInitialize != null) btnInitialize.onClick.AddListener(InitializeStore);
            if (btnBuyRemoveAds != null) btnBuyRemoveAds.onClick.AddListener(BuyRemoveAds);
            if (btnBuyCoins100 != null) btnBuyCoins100.onClick.AddListener(BuyCoins100);
            if (btnBuyCoins500 != null) btnBuyCoins500.onClick.AddListener(BuyCoins500);
            if (btnBuyCoins1000 != null) btnBuyCoins1000.onClick.AddListener(BuyCoins1000);
            if (btnBuyMonthSub != null) btnBuyMonthSub.onClick.AddListener(BuyMonthSub);
            if (btnRestorePurchases != null) btnRestorePurchases.onClick.AddListener(RestorePurchases);
            if (btnClearCache != null) btnClearCache.onClick.AddListener(ClearLocalCache);
            if (btnToggleFail != null) btnToggleFail.onClick.AddListener(ToggleSimulateFailure);

            SetupLogsScrollingRuntime();

            AddLog("IAP Test UI Started.");
            UpdateUI();
        }

        private void OnDestroy()
        {
            if (PurchaseManager.Instance != null)
            {
                PurchaseManager.Instance.OnPurchaseSucceeded -= OnPurchaseSucceeded;
                PurchaseManager.Instance.OnPurchaseFailed -= OnPurchaseFailed;
                PurchaseManager.Instance.OnPurchaseRestored -= OnPurchaseRestored;
                PurchaseManager.Instance.OnPurchasePendingOrUnknown -= OnPurchasePendingOrUnknown;
                PurchaseManager.Instance.OnProductsLoaded -= OnProductsLoaded;
            }
        }

        private void Update()
        {
            // Keep status texts updated in real-time
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (PurchaseManager.Instance == null || GameIAPHandler.Instance == null) return;

            bool isInit = PurchaseManager.Instance.IsInitialized;
            bool adsRemoved = GameIAPHandler.Instance.AreAdsRemoved();

            if (txtInitStatus != null)
            {
                txtInitStatus.text = isInit ? "<color=green>INITIALIZED</color>" : "<color=red>UNINITIALIZED</color>";
            }

            if (txtAdsStatus != null)
            {
                txtAdsStatus.text = adsRemoved ? "<color=green>ADS REMOVED (ACTIVE)</color>" : "<color=orange>ADS ACTIVE (NOT PURCHASED)</color>";
            }

            if (txtFailStatus != null)
            {
                txtFailStatus.text = EditorMockPurchaseService.ForceFailPurchases 
                    ? "<color=red>FAIL MODE ENABLED</color>" 
                    : "<color=green>NORMAL MODE</color>";
            }

            if (txtCoinsBalance != null)
            {
                txtCoinsBalance.text = $"Coins: <color=yellow>{GameIAPHandler.Instance.GetCoinsBalance()}</color>";
            }

            if (txtSubscriptionStatus != null)
            {
                var (isSubbed, expiration) = PurchaseManager.Instance.GetSubscriptionStatus("monthsub");
                if (isSubbed)
                {
                    txtSubscriptionStatus.text = $"Subscription: <color=green>ACTIVE</color> (Expires: <color=#00FFFF>{expiration:yyyy-MM-dd HH:mm:ss}</color>)";
                }
                else
                {
                    txtSubscriptionStatus.text = "Subscription: <color=red>INACTIVE</color>";
                }
            }

            // Throttle button interactions based on state
            if (btnBuyRemoveAds != null) btnBuyRemoveAds.interactable = isInit && !adsRemoved;
            if (btnBuyCoins100 != null) btnBuyCoins100.interactable = isInit;
            if (btnBuyCoins500 != null) btnBuyCoins500.interactable = isInit;
            if (btnBuyCoins1000 != null) btnBuyCoins1000.interactable = isInit; // Coins1000 is MS-managed consumable
            if (btnBuyMonthSub != null) btnBuyMonthSub.interactable = isInit && !PurchaseManager.Instance.IsPurchased("monthsub");
            if (btnRestorePurchases != null) btnRestorePurchases.interactable = isInit;
        }

        #region UI Button Triggers
        private void InitializeStore()
        {
            AddLog("Requesting store initialization...");
            PurchaseManager.Instance.Initialize();
        }
        private void BuyRemoveAds()
        {
            AddLog("Requesting purchase: removead...");
            PurchaseManager.Instance.Buy("removead");
        }

        private void BuyCoins100()
        {
            AddLog("Requesting purchase: coins100...");
            PurchaseManager.Instance.Buy("coins100");
        }

        private void BuyCoins500()
        {
            AddLog("Requesting purchase: coins500...");
            PurchaseManager.Instance.Buy("coins500");
        }

        private void BuyCoins1000()
        {
            AddLog("Requesting purchase: coins1000...");
            PurchaseManager.Instance.Buy("coins1000");
        }

        private void BuyMonthSub()
        {
            AddLog("Requesting purchase: monthsub...");
            PurchaseManager.Instance.Buy("monthsub");
        }

        private void RestorePurchases()
        {
            AddLog("Requesting purchase restoration...");
            PurchaseManager.Instance.RestorePurchases();
        }

        private void ToggleSimulateFailure()
        {
            EditorMockPurchaseService.ForceFailPurchases = !EditorMockPurchaseService.ForceFailPurchases;
            AddLog($"Simulated Failure mode set to: {EditorMockPurchaseService.ForceFailPurchases}");
        }

        private void ClearLocalCache()
        {
            // Check if Shift key is held down to determine if we also want to wipe the mock store server (supports both New and Old Input Systems)
            bool clearMockStore = false;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                clearMockStore = UnityEngine.InputSystem.Keyboard.current.leftShiftKey.isPressed || 
                                 UnityEngine.InputSystem.Keyboard.current.rightShiftKey.isPressed;
            }
#else
            clearMockStore = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif

            // Clear local game client cache (Ads and Coins)
            if (GameIAPHandler.Instance != null)
            {
                GameIAPHandler.Instance.ClearRemoveAdsState();
                GameIAPHandler.Instance.ClearCoins();
            }

            if (clearMockStore)
            {
                // Clear Mock Store expiration and mock purchases database
                PlayerPrefs.DeleteKey("MockIAP_SubExpiration_monthsub");
                if (PurchaseManager.Instance.ActiveService is EditorMockPurchaseService mockService)
                {
                    mockService.ClearAllMockPurchases();
                }
                PlayerPrefs.Save();
                // 标记 Shift + Clear Cache 为红色粗体，并输出极简的 7 个单词提示语
                AddLog("<b><color=red>Shift + Clear Cache</color></b>: Completely wipes local data and store purchases.");
            }
            else
            {
                // Temporarily suppress mock active licenses to simulate client-side cache wipe
                if (PurchaseManager.Instance.ActiveService is EditorMockPurchaseService mockService)
                {
                    mockService.SuppressMockLicenses();
                }
                PlayerPrefs.Save();
                // 标记 Clear Cache 为红色粗体，并输出极简的 7 个单词提示语
                AddLog("<b><color=red>Clear Cache</color></b>: Deletes local data but retains store purchases.");
            }

            UpdateUI();
        }
        #endregion

        #region PurchaseManager Event Callbacks
        private void OnPurchaseSucceeded(string productId)
        {
            AddLog($"<color=green>SUCCESS:</color> Purchased {productId}!");
        }

        private void OnPurchaseFailed(string productId, string reason)
        {
            AddLog($"<color=red>FAILED:</color> {productId} - Reason: {reason}");
        }

        private void OnPurchaseRestored(string productId)
        {
            AddLog($"<color=#00FFFF>RESTORED:</color> Unlocked {productId}!");
        }

        private void OnPurchasePendingOrUnknown(string productId, string status)
        {
            AddLog($"<color=yellow>PENDING/UNKNOWN:</color> {productId} - Status: {status}");
        }

        private void OnProductsLoaded()
        {
            AddLog("Store catalog loaded. Products available:");
            var products = PurchaseManager.Instance.GetProductInfos();
            foreach (var p in products)
            {
                AddLog($" - {p.InternalProductId} ({p.Title}): {p.FormattedPrice} [Owned: {p.IsOwned}]");
            }
        }
        #endregion

        private void AddLog(string msg)
        {
            string time = System.DateTime.Now.ToString("HH:mm:ss");
            logBuilder.AppendLine($"[{time}] {msg}");
            
            if (txtLogs != null)
            {
                txtLogs.text = logBuilder.ToString();
                
                // Automatically scroll to bottom
                if (scrollRect != null && gameObject.activeInHierarchy)
                {
                    StartCoroutine(ScrollToBottomCoroutine());
                }
            }
            Debug.Log($"[IAPUI] {msg}");
        }

        private void SetupLogsScrollingRuntime()
        {
            if (txtLogs == null) return;

            // Cache ScrollRect in parent
            scrollRect = txtLogs.GetComponentInParent<ScrollRect>();

            // Ensure wrapping is enabled so preferred height is calculated correctly
            txtLogs.textWrappingMode = TextWrappingModes.Normal;
            txtLogs.overflowMode = TextOverflowModes.Overflow;

            // Setup Layout and Fitter on Content (parent of Txt_Logs)
            if (txtLogs.transform.parent is RectTransform contentRect)
            {
                // Align anchors to Top-Stretch, pivot to Top
                contentRect.anchorMin = new Vector2(0, 1);
                contentRect.anchorMax = new Vector2(1, 1);
                contentRect.pivot = new Vector2(0.5f, 1);

                // Add or configure Vertical Layout Group
                var layoutGroup = contentRect.GetComponent<VerticalLayoutGroup>();
                if (layoutGroup == null)
                {
                    layoutGroup = contentRect.gameObject.AddComponent<VerticalLayoutGroup>();
                }
                layoutGroup.childControlWidth = true;
                layoutGroup.childControlHeight = true;
                layoutGroup.childForceExpandWidth = true;
                layoutGroup.childForceExpandHeight = false;

                // Add or configure Content Size Fitter
                var fitter = contentRect.GetComponent<ContentSizeFitter>();
                if (fitter == null)
                {
                    fitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
                }
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            }
        }

        private System.Collections.IEnumerator ScrollToBottomCoroutine()
        {
            // Wait until the end of the frame so the layout system has recalculated the Content height
            yield return new WaitForEndOfFrame();
            if (scrollRect != null)
            {
                scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }
}
