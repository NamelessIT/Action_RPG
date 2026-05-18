using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Quản lý màn hình chọn nhân vật (CharacterSelect scene).
/// Gắn script này vào một GameObject trong scene CharacterSelect.
/// Scene setup: 2 Button (Chris / Leo), 1 TextMeshPro hiển thị nhân vật đang chọn.
/// Khi nhấn "Bắt đầu" → lưu lựa chọn vào PlayerPrefs → load game scene.
/// </summary>
public class CharacterSelectManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button _chrisButton;
    [SerializeField] private Button _leoButton;
    [SerializeField] private Button _startButton;
    [SerializeField] private TextMeshProUGUI _selectedCharacterText;

    [Header("Scene")]
    [Tooltip("Tên scene game chính (phải có trong Build Settings)")]
    [SerializeField] private string _gameSceneName = "GameScene";

    [Header("Visual — highlight button đang chọn")]
    [SerializeField] private Color _selectedColor   = new Color(1f, 0.8f, 0.2f);
    [SerializeField] private Color _unselectedColor = Color.white;

    private string _selectedCharacter = "Chris"; // default

    private void Start()
    {
        // Đọc lại lựa chọn lần trước (nếu có)
        _selectedCharacter = PlayerPrefs.GetString("SelectedCharacter", "Chris");

        if (_chrisButton != null)
            _chrisButton.onClick.AddListener(() => SelectCharacter("Chris"));
        if (_leoButton != null)
            _leoButton.onClick.AddListener(() => SelectCharacter("Leo"));
        if (_startButton != null)
            _startButton.onClick.AddListener(StartGame);

        RefreshUI();
    }

    private void SelectCharacter(string characterId)
    {
        _selectedCharacter = characterId;
        RefreshUI();
    }

    private void RefreshUI()
    {
        if (_selectedCharacterText != null)
            _selectedCharacterText.text = $"Nhân vật: {_selectedCharacter}";

        // Highlight button đang chọn
        SetButtonColor(_chrisButton, _selectedCharacter == "Chris" ? _selectedColor : _unselectedColor);
        SetButtonColor(_leoButton,   _selectedCharacter == "Leo"   ? _selectedColor : _unselectedColor);
    }

    private void SetButtonColor(Button btn, Color color)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = color;
    }

    private void StartGame()
    {
        PlayerPrefs.SetString("SelectedCharacter", _selectedCharacter);
        PlayerPrefs.Save();
        Debug.Log($"[CharacterSelect] Bắt đầu với nhân vật: {_selectedCharacter}");
        SceneManager.LoadScene(_gameSceneName);
    }
}
