using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class AnimalProductClickHandler : MonoBehaviour
{
    private AnimalProductProducer producer;
    private Collider2D clickCollider;
    private int lastCollectAttemptFrame = -1;

    public void Configure(AnimalProductProducer configuredProducer)
    {
        producer = configuredProducer;
        clickCollider = GetComponent<Collider2D>();
        RefreshClickColliderFromVisuals();
    }

    private void Awake()
    {
        producer = GetComponent<AnimalProductProducer>();
        clickCollider = GetComponent<Collider2D>();
    }

    private void Update()
    {
        if (!WasPrimaryPointerPressedThisFrame(out Vector2 screenPosition) || IsPointerOverUi())
        {
            return;
        }

        Camera camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        if (camera == null)
        {
            return;
        }

        if (clickCollider == null)
        {
            clickCollider = GetComponent<Collider2D>();
        }

        if (clickCollider == null)
        {
            return;
        }

        RefreshClickColliderFromVisuals();

        Vector3 world = camera.ScreenToWorldPoint(screenPosition);
        Vector2 worldPoint = new(world.x, world.y);
        if (clickCollider.OverlapPoint(worldPoint))
        {
            TryCollect();
        }
    }

    private void OnMouseDown()
    {
        TryCollect();
    }

    private void TryCollect()
    {
        if (producer == null)
        {
            producer = GetComponent<AnimalProductProducer>();
        }

        if (producer == null || lastCollectAttemptFrame == Time.frameCount)
        {
            return;
        }

        lastCollectAttemptFrame = Time.frameCount;
        producer.CollectProduct(out _, out _);
    }

    public void RefreshClickColliderFromVisuals()
    {
        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        if (boxCollider == null)
        {
            return;
        }

        if (!TryGetActiveRendererBounds(out Bounds bounds))
        {
            boxCollider.offset = Vector2.zero;
            boxCollider.size = new Vector2(1.2f, 1.2f);
            clickCollider = boxCollider;
            return;
        }

        Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
        Vector3 localMin = transform.InverseTransformPoint(bounds.min);
        Vector3 localMax = transform.InverseTransformPoint(bounds.max);
        Vector3 localSize = localMax - localMin;

        const float padding = 0.18f;
        boxCollider.offset = new Vector2(localCenter.x, localCenter.y);
        boxCollider.size = new Vector2(
            Mathf.Max(0.8f, Mathf.Abs(localSize.x) + padding),
            Mathf.Max(0.8f, Mathf.Abs(localSize.y) + padding));

        clickCollider = boxCollider;
    }

    private bool TryGetActiveRendererBounds(out Bounds bounds)
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(false);
        bool hasBounds = false;
        bounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || renderer.sprite == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private static bool WasPrimaryPointerPressedThisFrame(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }
#endif

        screenPosition = default;
        return false;
    }

    private static bool IsPointerOverUi()
    {
        if (EventSystem.current == null)
        {
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            return EventSystem.current.IsPointerOverGameObject(Touchscreen.current.primaryTouch.touchId.ReadValue());
        }
#endif

        return EventSystem.current.IsPointerOverGameObject();
    }
}
