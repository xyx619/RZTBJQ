using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

// 技能数据类
[System.Serializable]
public class SkillData
{
    public string name;
    public float cooldown;
    public float damage;
    public Sprite icon;
    public string description;
    public GameObject effectPrefab;
}

// 角色数据类
[System.Serializable]
public class CharacterData
{
    public string name;
    public GameObject prefab;
    public List<SkillData> skills = new List<SkillData>();
}

// 配置数据库
[CreateAssetMenu(fileName = "SkillConfig", menuName = "Game/Skill Config")]
public class SkillConfig : ScriptableObject
{
    public List<CharacterData> characters = new List<CharacterData>();
}

// 运行时角色控制器（需添加到角色预制体上）
[ExecuteInEditMode]
public class CharacterController : MonoBehaviour
{
    public CharacterData data;
    private Dictionary<string, float> cooldownTimers = new Dictionary<string, float>();

    public void UseSkill(SkillData skill)
    {
        if (IsSkillOnCooldown(skill))
        {
            Debug.Log($"技能 {skill.name} 冷却中: {GetCooldownRemaining(skill):F1}s");
            return;
        }

        Debug.Log($"释放技能: {skill.name} (伤害: {skill.damage}, CD: {skill.cooldown}s)");

        // 应用技能效果
        if (skill.effectPrefab != null)
        {
            GameObject effect = Instantiate(skill.effectPrefab, transform.position, Quaternion.identity);
            Destroy(effect, 3f); // 3秒后销毁效果
        }

        // 启动冷却计时器
        cooldownTimers[skill.name] = skill.cooldown;
    }

    private bool IsSkillOnCooldown(SkillData skill)
    {
        return cooldownTimers.ContainsKey(skill.name) && cooldownTimers[skill.name] > 0;
    }

    private float GetCooldownRemaining(SkillData skill)
    {
        return cooldownTimers.ContainsKey(skill.name) ? cooldownTimers[skill.name] : 0;
    }

    private void Update()
    {
        // 更新冷却计时器
        foreach (var skillName in new List<string>(cooldownTimers.Keys))
        {
            cooldownTimers[skillName] -= Time.deltaTime;
            if (cooldownTimers[skillName] <= 0)
                cooldownTimers.Remove(skillName);
        }
    }
}

// 技能编辑器窗口
public class SkillEditor : EditorWindow
{
    private SkillConfig config;
    private CharacterData selectedCharacter;
    private SkillData selectedSkill;
    private Vector2 scrollPos;
    private string newCharacterName = "";
    private string newSkillName = "";
    private GameObject newCharacterPrefab;
    private string configPath = "Assets/SkillConfig.asset";
    private GameObject activeCharacterInstance;
    private Vector3 spawnPosition = Vector3.zero;

    [MenuItem("技能/技能编辑器")]
    public static void Open()
    {
        GetWindow<SkillEditor>("技能编辑器");
    }

    private void OnEnable()
    {
        LoadConfig();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        DestroyActiveCharacter();
    }

    private void OnGUI()
    {
        DrawToolbar();
        EditorGUILayout.BeginHorizontal();

        // 左侧面板：角色列表
        DrawCharacterList();

        // 中间面板：技能列表和生成功能
        if (selectedCharacter != null)
        {
            DrawSkillList();
            DrawSpawnControls();
        }

        // 右侧面板：技能属性
        if (selectedSkill != null)
        {
            DrawSkillProperties();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("加载配置", EditorStyles.toolbarButton))
            LoadConfig();

        if (GUILayout.Button("保存配置", EditorStyles.toolbarButton))
            SaveConfig();

        if (GUILayout.Button("重置", EditorStyles.toolbarButton))
            ResetEditor();

        EditorGUILayout.EndHorizontal();
    }

    private void DrawCharacterList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(200));
        EditorGUILayout.LabelField("角色列表", EditorStyles.boldLabel);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        if (config != null)
        {
            foreach (var character in config.characters)
            {
                if (GUILayout.Button(character.name))
                {
                    selectedCharacter = character;
                    selectedSkill = null;
                }
            }
        }

        EditorGUILayout.EndScrollView();

        // 添加新角色
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("创建新角色", EditorStyles.boldLabel);
        newCharacterName = EditorGUILayout.TextField("角色名称", newCharacterName);
        newCharacterPrefab = (GameObject)EditorGUILayout.ObjectField("角色预制体",
            newCharacterPrefab, typeof(GameObject), false);

        if (GUILayout.Button("添加角色") && !string.IsNullOrEmpty(newCharacterName))
        {
            AddNewCharacter();
        }

        if (selectedCharacter != null && GUILayout.Button("删除角色"))
        {
            DeleteSelectedCharacter();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSkillList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(200));
        EditorGUILayout.LabelField($"{selectedCharacter.name}的技能", EditorStyles.boldLabel);

        foreach (var skill in selectedCharacter.skills)
        {
            if (GUILayout.Button(skill.name))
            {
                selectedSkill = skill;
            }
        }

        // 添加新技能
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("添加新技能", EditorStyles.boldLabel);
        newSkillName = EditorGUILayout.TextField("技能名称", newSkillName);

        if (GUILayout.Button("添加技能") && !string.IsNullOrEmpty(newSkillName))
        {
            AddNewSkill();
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSpawnControls()
    {
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("场景生成", EditorStyles.boldLabel);

        spawnPosition = EditorGUILayout.Vector3Field("生成位置", spawnPosition);

        if (GUILayout.Button("在场景中生成角色"))
        {
            SpawnCharacter();
        }

        if (activeCharacterInstance != null && GUILayout.Button("销毁角色"))
        {
            DestroyActiveCharacter();
        }

        if (activeCharacterInstance != null && selectedSkill != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("技能测试", EditorStyles.boldLabel);

            if (GUILayout.Button($"释放技能: {selectedSkill.name}"))
            {
                TestSkill();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawSkillProperties()
    {
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField("技能属性", EditorStyles.boldLabel);

        selectedSkill.name = EditorGUILayout.TextField("名称", selectedSkill.name);
        selectedSkill.cooldown = EditorGUILayout.FloatField("冷却时间", selectedSkill.cooldown);
        selectedSkill.damage = EditorGUILayout.FloatField("伤害", selectedSkill.damage);
        selectedSkill.description = EditorGUILayout.TextArea("描述", selectedSkill.description);
        selectedSkill.icon = (Sprite)EditorGUILayout.ObjectField("图标",
            selectedSkill.icon, typeof(Sprite), false);
        selectedSkill.effectPrefab = (GameObject)EditorGUILayout.ObjectField("特效预制体",
            selectedSkill.effectPrefab, typeof(GameObject), false);

        if (GUILayout.Button("应用修改"))
        {
            SaveConfig();
        }

        if (GUILayout.Button("删除技能"))
        {
            DeleteSelectedSkill();
        }

        EditorGUILayout.EndVertical();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (activeCharacterInstance != null)
        {
            Handles.Label(activeCharacterInstance.transform.position + Vector3.up * 2,
                $"{selectedCharacter.name} (技能: {selectedCharacter.skills.Count})");
        }
    }

    private void LoadConfig()
    {
        config = AssetDatabase.LoadAssetAtPath<SkillConfig>(configPath);

        if (config == null)
        {
            config = CreateInstance<SkillConfig>();
            AssetDatabase.CreateAsset(config, configPath);
            AssetDatabase.SaveAssets();
        }
    }

    private void SaveConfig()
    {
        if (config != null)
        {
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("保存成功", "技能配置已保存", "确定");
        }
    }

    private void ResetEditor()
    {
        if (EditorUtility.DisplayDialog("确认重置", "确定要重置编辑器吗？所有未保存的更改将丢失。", "重置", "取消"))
        {
            selectedCharacter = null;
            selectedSkill = null;
            newCharacterName = "";
            newSkillName = "";
            newCharacterPrefab = null;
            DestroyActiveCharacter();
            LoadConfig();
        }
    }

    private void AddNewCharacter()
    {
        CharacterData newCharacter = new CharacterData
        {
            name = newCharacterName,
            prefab = newCharacterPrefab,
            skills = new List<SkillData>()
        };

        config.characters.Add(newCharacter);
        selectedCharacter = newCharacter;
        newCharacterName = "";
        newCharacterPrefab = null;

        SaveConfig();
    }

    private void DeleteSelectedCharacter()
    {
        if (EditorUtility.DisplayDialog("确认删除",
            $"确定要删除角色 '{selectedCharacter.name}' 及其所有技能吗？", "删除", "取消"))
        {
            if (activeCharacterInstance != null &&
                activeCharacterInstance.GetComponent<CharacterController>()?.data == selectedCharacter)
            {
                DestroyActiveCharacter();
            }

            config.characters.Remove(selectedCharacter);
            selectedCharacter = null;
            selectedSkill = null;
            SaveConfig();
        }
    }

    private void AddNewSkill()
    {
        SkillData newSkill = new SkillData
        {
            name = newSkillName,
            cooldown = 1f,
            damage = 10f,
            description = "新技能描述"
        };

        selectedCharacter.skills.Add(newSkill);
        selectedSkill = newSkill;
        newSkillName = "";

        SaveConfig();
    }

    private void DeleteSelectedSkill()
    {
        if (EditorUtility.DisplayDialog("确认删除",
            $"确定要删除技能 '{selectedSkill.name}' 吗？", "删除", "取消"))
        {
            selectedCharacter.skills.Remove(selectedSkill);
            selectedSkill = null;
            SaveConfig();
        }
    }

    private void SpawnCharacter()
    {
        if (selectedCharacter == null || selectedCharacter.prefab == null)
        {
            EditorUtility.DisplayDialog("错误", "请先选择角色并确保有预制体", "确定");
            return;
        }

        DestroyActiveCharacter();

        activeCharacterInstance = Instantiate(selectedCharacter.prefab, spawnPosition, Quaternion.identity);
        activeCharacterInstance.name = selectedCharacter.name;

        // 添加角色控制器
        CharacterController controller = activeCharacterInstance.GetComponent<CharacterController>();
        if (controller == null)
        {
            controller = activeCharacterInstance.AddComponent<CharacterController>();
        }

        controller.data = selectedCharacter;

        // 确保在编辑模式下也能运行
        if (!Application.isPlaying)
        {
            activeCharacterInstance.hideFlags = HideFlags.DontSaveInBuild;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        Selection.activeGameObject = activeCharacterInstance;
        SceneView.FrameLastActiveSceneView();

        Debug.Log($"角色 '{selectedCharacter.name}' 已生成到场景中000000000000");
    }

    private void DestroyActiveCharacter()
    {
        if (activeCharacterInstance != null)
        {
            DestroyImmediate(activeCharacterInstance);
            activeCharacterInstance = null;
        }
    }

    private void TestSkill()
    {
        if (activeCharacterInstance == null || selectedSkill == null) return;

        CharacterController controller = activeCharacterInstance.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.UseSkill(selectedSkill);
        }
    }
}