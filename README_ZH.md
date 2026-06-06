[English](README.md) | [简体中文]

# Unity Store Kit

`Unity Store Kit` 是一个专为 Unity 开发的微软原生 Windows 商店 (UWP) 内购 (IAP) 集成包。它封装了 Windows SDK 的 `StoreContext` 原生接口，提供了稳定可靠的商业化付费能力，同时支持在 Unity Editor 中进行完整的内购结账沙盒模拟（Mocking）。

---

## 🌟 核心功能

*   **微软商店原生集成**：在 UWP 导出平台（WSA）自动唤起微软商店付款结账对话框。
*   **三种主流商品类型**：全面支持永久非消耗品（Durable）、开发人员核销型消耗品（Consumable）以及自动续期订阅（Subscription）。
*   **漏单自动补发机制**：在初始化时，自动异步核销微软商店后台未履行完毕的历史漏单消耗品并安全补发道具，解决网络波动导致的丢单痛点。
*   **本地编辑器沙盒模拟**：无需打包导出，在 Unity Editor 中即会以弹窗对话框形式模拟微软商店付款，支持测试购买成功与失败流程。
*   **解耦的动态代码注册**：支持动态代码传参配置内购项（方法 A），不再强依赖 ScriptableObject 物理资源资产，更利于与已有框架集成和 CI/CD 自动构建。

---

## 🛠️ 安装方法 (Installation)

推荐在你的 Unity 工程中通过 Git 依赖方式直接导入：

打开你 Unity 项目的 `Packages/manifest.json` 文件，在 `dependencies` 中添加如下项：

```json
{
  "dependencies": {
    "com.maxjoygames.unitystorekit": "git+https://gitee.com/your-username/UnityStoreKit.git",
    ...
  }
}
```

*注意：请将上述链接替换为你将该包独立上传后的真实 Git 仓库地址。*

---

## 🚀 快速接入指引

### 方法 A：使用纯代码动态初始化（推荐，零资产依赖）

你可以不需要创建任何 `IAPCatalog` 资产文件，直接在代码中动态声明产品并完成初始化：

```csharp
using UnityEngine;
using System.Collections.Generic;
using UnityStoreKit; // 引入命名空间

public class GameIAPManager : MonoBehaviour
{
    private void Start()
    {
        // 1. 动态在代码中配置商品信息（可读取常量，也可由服务器动态下发）
        List<ProductDefinition> products = new List<ProductDefinition>
        {
            new ProductDefinition
            {
                internalId = "remove_ads",            // 游戏内通用 ID
                storeId = "9N4HFXJ46QGF",             // 微软 Partner Center 后台配置的 12 位 ID
                productType = ProductType.NonConsumable
            },
            new ProductDefinition
            {
                internalId = "coins_100",
                storeId = "9N9MQZS2C9L5",
                productType = ProductType.Consumable,
                mockTitle = "100 Gold Coins",         // Editor 模拟购买时显示的文案
                mockPrice = "$1.99"
            }
        };

        // 2. 绑定核心购买事件回调
        if (PurchaseManager.Instance != null)
        {
            PurchaseManager.Instance.OnPurchaseSucceeded += OnIapPurchaseSucceeded;
            PurchaseManager.Instance.OnPurchaseFailed += OnIapPurchaseFailed;
            PurchaseManager.Instance.OnPurchaseRestored += OnIapPurchaseRestored;
            PurchaseManager.Instance.OnProductsLoaded += OnIapProductsLoaded;

            // 3. 动态配置并启动服务
            PurchaseManager.Instance.Initialize(products);
        }
    }

    // 4. 发起购买请求
    public void BuyRemoveAds()
    {
        PurchaseManager.Instance.Buy("remove_ads");
    }

    // 5. 校验非消耗性商品拥有权
    public bool HasRemovedAds()
    {
        return PurchaseManager.Instance.IsPurchased("remove_ads");
    }

    private void OnIapPurchaseSucceeded(string productId)
    {
        Debug.Log($"[IAP] 商品 {productId} 购买并核销成功，正在分发道具！");
    }

    private void OnIapPurchaseFailed(string productId, string reason)
    {
        Debug.LogError($"[IAP] 商品 {productId} 购买失败：{reason}");
    }

    private void OnIapPurchaseRestored(string productId)
    {
        Debug.Log($"[IAP] 成功恢复已购商品权限：{productId}");
    }

    private void OnIapProductsLoaded()
    {
        // 此时可以通过 PurchaseManager.Instance.GetProductInfos() 获取到商店真实的本地化价格和标题
        var infos = PurchaseManager.Instance.GetProductInfos();
        foreach (var info in infos)
        {
            Debug.Log($"商品: {info.InternalProductId}, 本地化价格: {info.FormattedPrice}");
        }
    }
}
```

并在场景中的持久化节点（如全局管理器）上挂载 **`PurchaseManager`** 组件，在 Inspector 中确保 `Catalog` 字段为空并**取消勾选 `Initialize On Awake`**（因为我们将使用上述代码手动触发初始化）。

---

### 方法 B：使用 ScriptableObject 资产可视化配置

1.  在项目 `Assets` 目录下的任意位置，右键菜单选择 **Create -> IAP -> Store Catalog**，创建一个 `IAPCatalog` 配置文件。
2.  在 Inspector 中添加配置你的商品列表（映射 `internalId` 与 `storeId`）。
3.  在场景中创建一个 GameObject，挂载 **`PurchaseManager`** 脚本，并将你的 `IAPCatalog` 配置文件拖拽挂载到 `Catalog` 槽位，勾选 `Initialize On Awake`。
4.  在代码中通过 `PurchaseManager.Instance.Buy("your_id")` 直接使用。

---

## 🎮 导入测试 Demo 场景 (Samples)

本包提供了一套完善的交互式 UI 模拟面板，你可以直接在项目中进行沙盒调试：

1.  在 Unity 编辑器中打开菜单：**Window -> Package Manager**。
2.  在左侧列表中找到并选中 **Unity Store Kit**。
3.  在右侧面板的下方，展开 **Samples** 选项卡。
4.  点击 **IAP Demo Scene & UI** 右侧的 **Import** 按钮。
5.  导入完成后，双击打开 `Assets/Samples/Unity Store Kit/[Version]/IAP Demo Scene & UI/MainScene.unity` 场景。
6.  直接在 Editor 中点击 **Play**，即可完整体验模拟购买流程。
