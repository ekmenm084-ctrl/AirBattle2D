using UnityEngine;

/// <summary>
/// Düşman uçakları düştüğünde sahaya ganimet (Loot) bırakılmasını yönetir.
/// - Süper Ateş (Overdrive): İlk aşamalardan kaldırılmıştır! Yalnızca ilerleyen ve zorlaşan
///   dalgalarda (Dalga 10+ veya Boss Rush/Hardcore ileri aşamalarda) sahaya düşer.
/// - İlk aşamalarda oyuncunun hayatta kalması için Tamir (Health), Plazma Kalkanı (Shield)
///   ve Hangar Altını (Gold) dağıtılır.
/// </summary>
public class LootDropManager : MonoBehaviour
{
    public static void TryDropLoot(Vector3 position, bool isBoss = false)
    {
        // Normal uçakta %50 şans, Boss'ta kesin olarak (%100) 2-3 ganimet
        if (!isBoss && Random.value > 0.52f)
            return;

        int dropCount = isBoss ? Random.Range(2, 4) : 1;

        // Mevcut dalga numarasını ve geç/zor aşamada olup olmadığımızı belirle
        int currentWave = (GameManager.Instance != null) ? GameManager.Instance.currentPlaneIndex : 1;
        bool isLateWave = false;

        if (GameManager.Instance != null)
        {
            switch (GameManager.Instance.CurrentMode)
            {
                case GameManager.GameMode.BossRush:
                    isLateWave = (currentWave >= 3);
                    break;
                case GameManager.GameMode.Hardcore:
                    isLateWave = (currentWave >= 4);
                    break;
                case GameManager.GameMode.Endless:
                    isLateWave = (currentWave >= 8);
                    break;
                default: // SoloVsAI (Hikaye Seferi - 20 Dalga)
                    isLateWave = (currentWave >= 10);
                    break;
            }
        }
        else
        {
            isLateWave = (currentWave >= 10);
        }

        for (int i = 0; i < dropCount; i++)
        {
            Vector3 spawnPos = position;
            if (isBoss)
                spawnPos += new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-0.4f, 0.4f), 0f);

            GameObject lootObj = new GameObject("LootDrop_" + i);
            lootObj.transform.position = spawnPos;

            LootItem item = lootObj.AddComponent<LootItem>();

            float roll = Random.value;

            if (!isLateWave)
            {
                // İLK VE ERKEN AŞAMALAR: Süper Ateş (Overdrive) kesinlikle düşmez!
                // Sadece Can Tamiri, Plazma Kalkanı ve Altın düşer.
                if (roll < 0.38f)
                    item.type = LootItem.LootType.Health;
                else if (roll < 0.72f)
                    item.type = LootItem.LootType.Shield;
                else
                    item.type = LootItem.LootType.Gold;
            }
            else
            {
                // İLERİ VE ZORLAYICI AŞAMALAR: Çatışma kızıştığında Süper Ateş sahneye çıkar!
                // Can, Kalkan, Süper Ateş (Overdrive) ve Altın.
                if (roll < 0.28f)
                    item.type = LootItem.LootType.Health;
                else if (roll < 0.54f)
                    item.type = LootItem.LootType.Shield;
                else if (roll < 0.76f)
                    item.type = LootItem.LootType.Overdrive; // %22 Süper Ateş şansı
                else
                    item.type = LootItem.LootType.Gold;
            }
        }

        // ==========================================
        // 20+ SEVİYE BOSS SANDIK ANAHTARI DÜŞÜŞÜ (1/10 İhtimal)
        // ==========================================
        // 20. seviyeden sonra (veya 20. dalgadan sonra) herhangi bir boss düştüğünde
        // 1/10 (%10) toplam ihtimalle rastgele anahtar düşer:
        // - En nadir: Altın Anahtar (1/100 - %1)
        // - Gümüş Anahtar (3/100 - %3)
        // - Bronz Anahtar (6/100 - %6)
        // Düşme sırası tamamen rastgeledir; ilk boss'ta bile Altın Anahtar düşebilir!
        if (isBoss)
        {
            int pilotLvl = PlayerProfileManager.EnsureInstance()?.level ?? 1;
            int currentWaveIndex = (GameManager.Instance != null) ? GameManager.Instance.currentPlaneIndex : 1;

            if (pilotLvl >= 20 || currentWaveIndex >= 20)
            {
                float keyRoll = Random.value;
                if (keyRoll < 0.10f) // 1/10 Genel Anahtar İhtimali
                {
                    GameObject keyLootObj = new GameObject("BossKeyDrop");
                    keyLootObj.transform.position = position + new Vector3(0f, 0.45f, 0f);
                    LootItem keyItem = keyLootObj.AddComponent<LootItem>();

                    if (keyRoll < 0.01f) // 1/100 Şans ile Altın Anahtar!
                    {
                        keyItem.type = LootItem.LootType.GoldKey;
                    }
                    else if (keyRoll < 0.04f) // 3/100 Şans ile Gümüş Anahtar
                    {
                        keyItem.type = LootItem.LootType.SilverKey;
                    }
                    else // 6/100 Şans ile Bronz Anahtar
                    {
                        keyItem.type = LootItem.LootType.BronzeKey;
                    }
                }
            }
        }
    }
}

