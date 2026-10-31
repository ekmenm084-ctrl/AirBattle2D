using UnityEngine;

/// <summary>
/// Uçağın yatay düzlemde (sağ-sol) hareketini yönetir.
/// P1: A - D ve Sanal Joystick
/// P2: Sol Ok (◀) - Sağ Ok (▶) ve İki Oyunculu mod
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlaneController : MonoBehaviour
{
    [Header("Hareket Ayarları")]
    public float moveSpeed = 7f;          // Sağa/sola hareket hızı
    public bool autoDetectLimits = true;  // Ekran sınırlarını kameradan otomatik al

    [Header("Ekran Sınırları")]
    public float minX = -7.5f;
    public float maxX = 7.5f;
    public float fixedY = 0f;

    [Header("Oyuncu Ayarları")]
    public bool isPlayer1 = true;
    public VirtualJoystick joystick;

    private Rigidbody2D _rb;
    private TwoPlayerInputRouter _p2Router;
    private Vector2 _inputDir;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        _rb.linearDamping = 2f;
        _rb.constraints = RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;
    }

    private void Start()
    {
        _p2Router = FindObjectOfType<TwoPlayerInputRouter>();
        fixedY = transform.position.y;

        if (isPlayer1)
            transform.rotation = Quaternion.Euler(0, 0, 0);
        else
            transform.rotation = Quaternion.Euler(0, 0, 180);

        if (autoDetectLimits && Camera.main != null)
        {
            float vertExtent = Camera.main.orthographicSize;
            float horzExtent = vertExtent * (float)Screen.width / Screen.height;
            minX = -horzExtent + 0.8f;
            maxX = horzExtent - 0.8f;
        }

        if (isPlayer1 && joystick == null)
        {
            var joysticks = FindObjectsOfType<VirtualJoystick>();
            foreach (var j in joysticks)
            {
                if (j.isPlayer1)
                {
                    joystick = j;
                    break;
                }
            }
        }
    }

    private void Update()
    {
        ReadInput();
    }

    private void FixedUpdate()
    {
        ApplyMovement();
    }

    private void LateUpdate()
    {
        ClampToScreen();
    }

    private void ReadInput()
    {
        bool isTwoPlayer = (GameSettings.SelectedMode == GameSettings.GameMode.Online) ||
                           (GameManager.Instance != null && GameManager.Instance.CurrentMode == GameManager.GameMode.TwoPlayer);

        if (isPlayer1)
        {
            float horizontal = 0f;

            // P1 Klavye: A - D tuşları (Solo ise Ok tuşları da çalışır)
            try
            {
                if (Input.GetKey(KeyCode.A)) horizontal -= 1f;
                if (Input.GetKey(KeyCode.D)) horizontal += 1f;

                if (!isTwoPlayer)
                {
                    if (Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
                    if (Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
                }
            }
            catch { }

            if (Mathf.Abs(horizontal) > 0.01f)
            {
                _inputDir = new Vector2(horizontal, 0f);
            }
            else if (joystick != null && Mathf.Abs(joystick.InputDirection.x) > 0.05f)
            {
                _inputDir = new Vector2(joystick.InputDirection.x, 0f);
            }
            else
            {
                _inputDir = Vector2.zero;
            }
        }
        else
        {
            // OYUNCU 2 / DÜŞMAN KONTROLLERİ:
            // Klavyeden Sol-Sağ ok tuşları VEYA ekrandaki Düşman Joystick'i ile kontrol edilir
            float horizontal = 0f;

            try
            {
                if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.Keypad4) || Input.GetKey(KeyCode.J)) horizontal -= 1f;
                if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.Keypad6) || Input.GetKey(KeyCode.L)) horizontal += 1f;
            }
            catch { }

            if (_p2Router == null)
                _p2Router = FindObjectOfType<TwoPlayerInputRouter>();

            Vector2 p2In = _p2Router != null ? _p2Router.GetP2Input() : Vector2.zero;

            if (Mathf.Abs(horizontal) > 0.01f)
            {
                _inputDir = new Vector2(horizontal, 0f);
            }
            else if (Mathf.Abs(p2In.x) > 0.05f)
            {
                // Ekrandaki Düşman joystick'ine dokunuluyorsa uçağı hareket ettir
                _inputDir = new Vector2(p2In.x, 0f);
            }
            else if (isTwoPlayer)
            {
                _inputDir = Vector2.zero;
            }
            // Solo modda joystick serbestken Yapay Zeka (AIController) SetAIInput çağırarak yönlendirir
        }
    }

    public void SetAIInput(Vector2 input)
    {
        _inputDir = new Vector2(input.x, 0f);
    }

    private void ApplyMovement()
    {
        _rb.linearVelocity = new Vector2(_inputDir.x * moveSpeed, 0f);
    }

    private void ClampToScreen()
    {
        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.y = fixedY;
        transform.position = pos;
    }

    public void FreezeMovement()
    {
        _rb.linearVelocity = Vector2.zero;
        enabled = false;
    }
}
