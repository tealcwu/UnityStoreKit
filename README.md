[English] | [简体中文](README_ZH.md)

# Unity Store Kit

`Unity Store Kit` is a lightweight, reusable Unity Package (UPM) for native Microsoft Windows Store (UWP) In-App Purchases (IAP). It encapsulates Windows SDK's native `StoreContext` interfaces, providing robust billing capabilities while supporting a fully functional billing sandbox simulation (mocking) within the Unity Editor.

---

## 🌟 Key Features

*   **Native Windows Store Integration**: Automatically prompts the official Microsoft Store purchase checkout dialogs on the Universal Windows Platform (UWP/WSA).
*   **Three Major Product Types**: Full support for Durables (lifetime/non-consumables), Developer-managed Consumables (consumables), and auto-renewable Subscriptions.
*   **Loss Prevention & Auto-Recovery**: Asynchronously queries and completes unconsumed consumable transactions upon initialization, resolving transaction loss due to network interruptions.
*   **Editor Sandbox Mocking**: Fully simulates checkout dialogue flows directly in the Unity Editor without exporting, facilitating verification of purchase success and failure events.
*   **Decoupled Dynamic Code Setup**: Initialize products dynamically via code (Method A). Ideal for programmatic frameworks and CI/CD automation without relying on physical ScriptableObject assets.

---

## 🛠️ Installation

It is recommended to import this package via Git URL dependency:

Open your Unity project's `Packages/manifest.json` and append the dependency:

```json
{
  "dependencies": {
    "com.maxjoygames.unitystorekit": "git+https://github.com/your-username/UnityStoreKit.git",
    ...
  }
}
```

*Note: Replace the URL above with the actual Git repository address where you host the independent package.*

---

## 🚀 Quick Start Guide

### Method A: Programmatic Dynamic Initialization (Recommended)

You can define your product catalogs dynamically in scripts and initialize the billing manager directly without creating any physical `IAPCatalog` assets:

```csharp
using UnityEngine;
using System.Collections.Generic;
using UnityStoreKit; // Import Namespace

public class GameIAPManager : MonoBehaviour
{
    private void Start()
    {
        // 1. Configure products dynamically in code (constants or server configuration)
        List<ProductDefinition> products = new List<ProductDefinition>
        {
            new ProductDefinition
            {
                internalId = "remove_ads",            // Game-internal ID
                storeId = "9N4HFXJ46QGF",             // 12-char alphanumeric Microsoft Store ID
                productType = ProductType.NonConsumable
            },
            new ProductDefinition
            {
                internalId = "coins_100",
                storeId = "9N9MQZS2C9L5",
                productType = ProductType.Consumable,
                mockTitle = "100 Gold Coins",         // Title shown during editor mocking
                mockPrice = "$1.99"                   // Price shown during editor mocking
            }
        };

        // 2. Register callback listeners
        if (PurchaseManager.Instance != null)
        {
            PurchaseManager.Instance.OnPurchaseSucceeded += OnIapPurchaseSucceeded;
            PurchaseManager.Instance.OnPurchaseFailed += OnIapPurchaseFailed;
            PurchaseManager.Instance.OnPurchaseRestored += OnIapPurchaseRestored;
            PurchaseManager.Instance.OnProductsLoaded += OnIapProductsLoaded;

            // 3. Initialize billing service
            PurchaseManager.Instance.Initialize(products);
        }
    }

    // 4. Initiate checkout request
    public void BuyRemoveAds()
    {
        PurchaseManager.Instance.Buy("remove_ads");
    }

    // 5. Query ownership state for durables/subscriptions
    public bool HasRemovedAds()
    {
        return PurchaseManager.Instance.IsPurchased("remove_ads");
    }

    private void OnIapPurchaseSucceeded(string productId)
    {
        Debug.Log($"[IAP] Successfully purchased and fulfilled: {productId}. Distributing rewards!");
    }

    private void OnIapPurchaseFailed(string productId, string reason)
    {
        Debug.LogError($"[IAP] Purchase failed for {productId}: {reason}");
    }

    private void OnIapPurchaseRestored(string productId)
    {
        Debug.Log($"[IAP] Restored ownership for: {productId}");
    }

    private void OnIapProductsLoaded()
    {
        // Now you can call PurchaseManager.Instance.GetProductInfos() to retrieve localized prices from Store
        var infos = PurchaseManager.Instance.GetProductInfos();
        foreach (var info in infos)
        {
            Debug.Log($"Product: {info.InternalProductId}, Price: {info.FormattedPrice}");
        }
    }
}
```

Ensure the **`PurchaseManager`** component is attached to a persistent GameObject in your scene. Leave the `Catalog` slot empty in the Inspector and **uncheck `Initialize On Awake`** (since it will be manually triggered in code).

---

### Method B: Visualization Config via ScriptableObject

1.  Right-click anywhere in `Assets` directory and choose **Create -> IAP -> Store Catalog** to create an `IAPCatalog` asset file.
2.  Configure your product definitions (mapping `internalId` and `storeId`) in the Inspector.
3.  Attach **`PurchaseManager`** script to a GameObject, assign your catalog asset to the `Catalog` field, and check `Initialize On Awake`.
4.  Trigger purchases from scripts using `PurchaseManager.Instance.Buy("your_id")`.

---

## 🎮 Importing Test Sandbox Demo (Samples)

This package contains an interactive demo panel to speed up integration:

1.  In the Unity Editor, open **Window -> Package Manager**.
2.  Select **Unity Store Kit** from the package list.
3.  Scroll down the details view and expand the **Samples** section.
4.  Click the **Import** button next to **IAP Demo Scene & UI**.
5.  Double-click to open `Assets/Samples/Unity Store Kit/[Version]/IAP Demo Scene & UI/MainScene.unity`.
6.  Press **Play** in the editor to simulate the complete billing transaction flow.
