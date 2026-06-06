using System;
using UnityEngine;

namespace UnityStoreKit
{
    [DisallowMultipleComponent]
    public sealed class GameIAPHandler : MonoBehaviour
    {
        public static GameIAPHandler Instance { get; private set; }

        private const string RemoveAdsPrefsKey = "Game_RemoveAds_Unlocked";
        private const string CoinsBalancePrefsKey = "Game_Coins_Balance";

        public event Action OnStateUpdated;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (PurchaseManager.Instance != null)
            {
                PurchaseManager.Instance.OnPurchaseSucceeded += OnPurchaseSucceeded;
                PurchaseManager.Instance.OnPurchaseRestored += OnPurchaseRestored;
                PurchaseManager.Instance.OnProductsLoaded += OnProductsLoaded;
            }
        }

        private void OnDestroy()
        {
            if (PurchaseManager.Instance != null)
            {
                PurchaseManager.Instance.OnPurchaseSucceeded -= OnPurchaseSucceeded;
                PurchaseManager.Instance.OnPurchaseRestored -= OnPurchaseRestored;
                PurchaseManager.Instance.OnProductsLoaded -= OnProductsLoaded;
            }
        }

        private void OnPurchaseSucceeded(string productId)
        {
            ProcessReward(productId);
        }

        private void OnPurchaseRestored(string productId)
        {
            // Restore persistent non-consumables (like RemoveAds)
            if (productId == "removead")
            {
                SaveLocalRemoveAdsState(true);
            }
        }

        private void OnProductsLoaded()
        {
            // Auto-synchronize local RemoveAds cache if initialization loads active ownership
            if (PurchaseManager.Instance.IsPurchased("removead"))
            {
                SaveLocalRemoveAdsState(true);
            }
            OnStateUpdated?.Invoke();
        }

        private void ProcessReward(string productId)
        {
            if (productId == "removead")
            {
                SaveLocalRemoveAdsState(true);
            }
            else if (productId == "coins100")
            {
                AddCoins(100);
            }
            else if (productId == "coins500")
            {
                AddCoins(500);
            }
            else if (productId == "coins1000")
            {
                AddCoins(1000);
            }
            OnStateUpdated?.Invoke();
        }

        private void SaveLocalRemoveAdsState(bool isRemoved)
        {
            PlayerPrefs.SetInt(RemoveAdsPrefsKey, isRemoved ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log($"[GameIAPHandler] Local Ads Removed state updated to: {isRemoved}");
            OnStateUpdated?.Invoke();
        }

        /// <summary>
        /// Check if ads are removed (either cached locally or confirmed by store)
        /// </summary>
        public bool AreAdsRemoved()
        {
            return PlayerPrefs.GetInt(RemoveAdsPrefsKey, 0) == 1 || 
                   (PurchaseManager.Instance != null && PurchaseManager.Instance.IsPurchased("removead"));
        }

        public int GetCoinsBalance()
        {
            return PlayerPrefs.GetInt(CoinsBalancePrefsKey, 0);
        }

        public void AddCoins(int amount)
        {
            int current = GetCoinsBalance();
            PlayerPrefs.SetInt(CoinsBalancePrefsKey, current + amount);
            PlayerPrefs.Save();
            Debug.Log($"[GameIAPHandler] Added {amount} coins. New balance: {GetCoinsBalance()}");
            OnStateUpdated?.Invoke();
        }

        public void ClearCoins()
        {
            PlayerPrefs.DeleteKey(CoinsBalancePrefsKey);
            PlayerPrefs.Save();
            Debug.Log("[GameIAPHandler] Coins balance cleared.");
            OnStateUpdated?.Invoke();
        }

        public void ClearRemoveAdsState()
        {
            PlayerPrefs.DeleteKey(RemoveAdsPrefsKey);
            PlayerPrefs.Save();
            Debug.Log("[GameIAPHandler] Local Ads state cleared.");
            OnStateUpdated?.Invoke();
        }
    }
}
