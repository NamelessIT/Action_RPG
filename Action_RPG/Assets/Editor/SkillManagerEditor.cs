using UnityEngine;
using UnityEditor; // Bắt buộc phải có thư viện này

[CustomEditor(typeof(SkillManager))] // Báo cho Unity biết script này dùng để vẽ giao diện cho SkillManager
public class SkillManagerEditor : Editor
{
    // Hàm này sẽ chạy mỗi khi bạn click vào GameObject có gắn SkillManager
    public override void OnInspectorGUI()
    {
        // 1. Cập nhật dữ liệu mới nhất từ object
        serializedObject.Update();

        // 2. Lấy các thuộc tính từ script gốc ra để xử lý
        SerializedProperty isPlayerProp = serializedObject.FindProperty("isPlayer");

        // Các thuộc tính của Player
        SerializedProperty currentDefaultPassiveProp = serializedObject.FindProperty("currentDefaultPassive");
        SerializedProperty currentPassive1Prop = serializedObject.FindProperty("currentPassive1");
        SerializedProperty currentPassive2Prop = serializedObject.FindProperty("currentPassive2");
        SerializedProperty currentSkillProp = serializedObject.FindProperty("currentSkill");
        SerializedProperty currentSignatureProp = serializedObject.FindProperty("currentSignature");
        SerializedProperty pickUpSkillProp = serializedObject.FindProperty("pickUpSkill");

        // Các thuộc tính của Enemy
        SerializedProperty enemySkillsProp = serializedObject.FindProperty("enemySkills");

        // --- BẮT ĐẦU VẼ GIAO DIỆN ---

        // Vẽ cái nút tick "Is Player" đầu tiên
        EditorGUILayout.PropertyField(isPlayerProp);

        EditorGUILayout.Space(10); // Tạo khoảng trống cho thoáng

        // LOGIC ẨN HIỆN Ở ĐÂY:
        if (isPlayerProp.boolValue == true)
        {
            // Nếu là Player -> Vẽ giao diện Player
            EditorGUILayout.LabelField("Player Slots", EditorStyles.boldLabel);

            // Vẽ khung box bao quanh cho đẹp (tùy chọn)
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.PropertyField(currentDefaultPassiveProp);
                EditorGUILayout.PropertyField(currentPassive1Prop);
                EditorGUILayout.PropertyField(currentPassive2Prop);
                EditorGUILayout.PropertyField(currentSkillProp);
                EditorGUILayout.PropertyField(currentSignatureProp);

                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Debug / Test", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(pickUpSkillProp);
            }
            EditorGUILayout.EndVertical();
        }
        else
        {
            // Nếu là Enemy -> Vẽ giao diện Enemy
            EditorGUILayout.LabelField("Enemy Configuration", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.PropertyField(enemySkillsProp);

                // Gợi ý: Có thể thêm nút bấm test skill cho Enemy tại đây nếu muốn
                if (GUILayout.Button("Random Test Skill"))
                {
                    Debug.Log("Nút này để test logic Enemy sau này");
                }
            }
            EditorGUILayout.EndVertical();
        }

        // 3. Lưu lại các thay đổi (Nếu không có dòng này, chỉnh sửa sẽ không được lưu)
        serializedObject.ApplyModifiedProperties();
    }
}