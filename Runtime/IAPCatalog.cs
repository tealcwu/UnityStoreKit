using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityStoreKit
{
    public enum ProductType
    {
        Consumable,      // 消耗性商品（可重复购买，例如金币）
        NonConsumable,  // 非消耗性商品（一次性买断，例如去广告）
        Subscription     // 订阅（例如月卡、季卡等）
    }

    [System.Serializable]
    public class ProductDefinition
    {
        [Tooltip("游戏内部的通用商品唯一标识 ID，例如 remove_ads")]
        public string internalId;

        [Tooltip("微软 Partner Center 后台配置的 12 位 alphanumeric Store ID，例如 9N4HFXJ46QGF")]
        public string storeId;

        [Tooltip("商品的销售类型")]
        public ProductType productType;

        [Header("Mock Editor Settings (仅在编辑器模拟时显示在测试UI)")]
        public string mockTitle;
        public string mockDescription;
        public string mockPrice;
    }

    [CreateAssetMenu(fileName = "IAPCatalog", menuName = "IAP/Store Catalog", order = 1)]
    public class IAPCatalog : ScriptableObject
    {
        [Tooltip("商品列表配置")]
        public List<ProductDefinition> products = new List<ProductDefinition>();

        /// <summary>
        /// 根据游戏内商品ID查找定义
        /// </summary>
        public ProductDefinition GetByInternalId(string internalId)
        {
            return products.Find(p => p.internalId == internalId);
        }

        /// <summary>
        /// 根据微软商店ID查找定义
        /// </summary>
        public ProductDefinition GetByStoreId(string storeId)
        {
            return products.Find(p => p.storeId == storeId);
        }
    }
}
