using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DraggableItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image _iconImage;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private CanvasGroup _canvasGroup;

    private InventoryItemRecord _item;
    private InventoryController _owner;
    private RectTransform _ghostRect;
    private Image _ghostImage;
    private Canvas _rootCanvas;

    public InventoryItemRecord Item => _item;

    public void Bind(InventoryItemRecord item, InventoryController owner)
    {
        _item = item;
        _owner = owner;

        bool hasIcon = item != null && item.Icon != null;

        if (_iconImage != null)
        {
            _iconImage.sprite  = hasIcon ? item.Icon : null;
            _iconImage.enabled = true;
            _iconImage.color   = hasIcon
                ? Color.white
                : new Color(0.55f, 0.55f, 0.55f, 0.6f);
        }
        else
        {
            Debug.LogWarning($"[DraggableItem] _iconImage chưa được gán trên prefab '{gameObject.name}'. " +
                             "Mở DraggableItem prefab → gán Image component vào field _iconImage.");
        }

        if (_nameText != null)
        {
            _nameText.text = item != null ? item.DisplayName : string.Empty;
        }
        else
        {
            Debug.LogWarning($"[DraggableItem] _nameText chưa được gán trên prefab '{gameObject.name}'. " +
                             "Mở DraggableItem prefab → gán TextMeshProUGUI vào field _nameText.");
        }

        gameObject.name = item != null ? $"InventoryItem_{item.DisplayName}" : "InventoryItem_Empty";

        Debug.Log($"[DraggableItem] Bind '{item?.DisplayName}' | icon:{hasIcon} | " +
                  $"iconImg:{_iconImage != null} | nameText:{_nameText != null} | " +
                  $"active:{gameObject.activeSelf}");
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_item == null || _owner == null || !_item.IsEquippable)
        {
            return;
        }

        _rootCanvas = _owner.GetRootCanvas();
        if (_rootCanvas == null)
        {
            return;
        }

        InventoryDragContext.BeginFromInventory(_item, this);

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0.35f;
            _canvasGroup.blocksRaycasts = false;
        }

        CreateGhostIcon();
        UpdateGhostPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!InventoryDragContext.HasActiveDrag)
        {
            return;
        }

        UpdateGhostPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
        }

        DestroyGhostIcon();
        InventoryDragContext.Clear();
    }

    private void CreateGhostIcon()
    {
        GameObject ghostObject = new GameObject("DragGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        ghostObject.transform.SetParent(_rootCanvas.transform, false);

        _ghostRect = ghostObject.GetComponent<RectTransform>();
        _ghostRect.sizeDelta = new Vector2(72f, 72f);
        _ghostImage = ghostObject.GetComponent<Image>();
        _ghostImage.raycastTarget = false;
        _ghostImage.sprite = _item.Icon;
        _ghostImage.preserveAspect = true;

        CanvasGroup ghostCanvasGroup = ghostObject.GetComponent<CanvasGroup>();
        ghostCanvasGroup.blocksRaycasts = false;
        ghostCanvasGroup.alpha = 0.85f;
    }

    private void UpdateGhostPosition(PointerEventData eventData)
    {
        if (_ghostRect == null)
        {
            return;
        }

        _ghostRect.position = eventData.position;
    }

    private void DestroyGhostIcon()
    {
        if (_ghostRect != null)
        {
            Destroy(_ghostRect.gameObject);
            _ghostRect = null;
            _ghostImage = null;
        }
    }
}