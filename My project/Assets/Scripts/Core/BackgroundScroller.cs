using UnityEngine;

/// <summary>
/// Tek parça, sabit ve kusursuz arka plan.
/// Tekrar etme, ek çizgileri veya kayma olmadan tüm ekranı tam kadraj kaplar.
/// </summary>
public class BackgroundScroller : MonoBehaviour
{
    private void Start()
    {
        // Tam ekran merkezine sabitle
        transform.position = new Vector3(0f, 0f, 2f);

        if (Camera.main != null)
        {
            float cameraHeight = Camera.main.orthographicSize * 2f;
            float cameraWidth = cameraHeight * (float)Screen.width / Screen.height;

            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                float spriteW = sr.sprite.rect.width / sr.sprite.pixelsPerUnit;
                float spriteH = sr.sprite.rect.height / sr.sprite.pixelsPerUnit;

                // Ekranı kenarlarda hiç boşluk bırakmadan tam kaplayacak ölçek
                float scaleX = cameraWidth / spriteW;
                float scaleY = cameraHeight / spriteH;
                float maxScale = Mathf.Max(scaleX, scaleY) * 1.02f;

                transform.localScale = new Vector3(maxScale, maxScale, 1f);
            }
        }
    }
}
