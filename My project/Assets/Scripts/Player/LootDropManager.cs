using UnityEngine;

/// <summary>
/// Düşman uçakları düştüğünde sahaya ganimet (Loot) bırakılmasını yönetir.
/// </summary>
public class LootDropManager : MonoBehaviour
{
    public static void TryDropLoot(Vector3 position, bool isBoss = false)
    {
        // Normal uçakta %50 şans, Boss'ta kesin olarak (%100) 2-3 ganimet
        if (!isBoss && Random.value > 0.52f)
            return;

        int dropCount = isBoss ? Random.Range(2, 4) : 1;

        for (int i = 0; i < dropCount; i++)
        {
            Vector3 spawnPos = position;
            if (isBoss)
                spawnPos += new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-0.4f, 0.4f), 0f);

            GameObject lootObj = new GameObject("LootDrop_" + i);
            lootObj.transform.position = spawnPos;

            LootItem item = lootObj.AddComponent<LootItem>();

            // Rastgele ganimet tipi seç
            float roll = Random.value;
            if (roll < 0.28f)
                item.type = LootItem.LootType.Health;
            else if (roll < 0.52f)
                item.type = LootItem.LootType.Shield;
            else if (roll < 0.76f)
                item.type = LootItem.LootType.Overdrive;
            else
                item.type = LootItem.LootType.Gold;
        }
    }
}

