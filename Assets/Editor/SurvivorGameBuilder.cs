// Tự động dựng toàn bộ game Survivor Shooter từ asset có sẵn:
//   Tools > Survivor Shooter > Build Game
// Tạo: Animator Controller, prefab Player/Zombunny/ZomBear/Hellephant, UI (HUD + Game Over),
//      scene Assets/Scenes/Main.unity, bake NavMesh, thêm scene vào Build Settings.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class SurvivorGameBuilder
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    const string CharPrefabDir = "Assets/Prefabs/Characters";
    const string AnimDir = "Assets/Animation";

    class EnemySpec
    {
        public string name, fbx, hurt, death;
        public int hp, damage, score;
        public float speed, capsuleRadius, attackRadius, stopDistance;
        public float spawnTime, minSpawnTime, ramp;
    }

    static readonly EnemySpec[] Enemies =
    {
        new EnemySpec { name = "Zombunny",  fbx = "Zombunny.fbx",  hurt = "ZomBunny Hurt.wav",  death = "ZomBunny Death.wav",
                        hp = 100, damage = 10, score = 10, speed = 3.0f, capsuleRadius = 0.30f, attackRadius = 0.9f, stopDistance = 0.5f,
                        spawnTime = 3f,  minSpawnTime = 1.2f, ramp = 0.05f },
        new EnemySpec { name = "ZomBear",   fbx = "ZomBear.fbx",   hurt = "ZomBear Hurt.wav",   death = "ZomBear Death.wav",
                        hp = 150, damage = 15, score = 20, speed = 2.6f, capsuleRadius = 0.35f, attackRadius = 1.0f, stopDistance = 0.6f,
                        spawnTime = 8f,  minSpawnTime = 3.5f, ramp = 0.15f },
        new EnemySpec { name = "Hellephant", fbx = "Hellephant.fbx", hurt = "Hellephant Hurt.wav", death = "Hellephant Death.wav",
                        hp = 400, damage = 25, score = 50, speed = 2.0f, capsuleRadius = 0.60f, attackRadius = 1.4f, stopDistance = 0.9f,
                        spawnTime = 20f, minSpawnTime = 9f,   ramp = 0.5f },
    };

    [MenuItem("Tools/Survivor Shooter/Build Game (Scene + Prefabs + UI)")]
    public static void BuildGame()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Survivor Shooter", "Hãy thoát Play Mode trước khi build.", "OK");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Survivor Shooter", "Scene " + ScenePath + " đã tồn tại và sẽ bị ghi đè. Tiếp tục?", "Tiếp tục", "Huỷ"))
            return;

        try
        {
            EditorUtility.DisplayProgressBar("Survivor Shooter", "Chuẩn bị thư mục & layer...", 0.05f);
            foreach (string d in new[] { "Assets/Scenes", CharPrefabDir, AnimDir }) Directory.CreateDirectory(d);
            AssetDatabase.Refresh();

            int floorLayer = EnsureLayer("Floor");
            int shootableLayer = EnsureLayer("Shootable");

            EditorUtility.DisplayProgressBar("Survivor Shooter", "Tạo Animator Controller...", 0.2f);
            var playerCtrl = BuildPlayerController(LoadClips("Assets/Models/Characters/Player.fbx"));
            var enemyCtrls = new Dictionary<string, AnimatorController>();
            foreach (var e in Enemies)
                enemyCtrls[e.name] = BuildEnemyController(e.name, LoadClips("Assets/Models/Characters/" + e.fbx));

            EditorUtility.DisplayProgressBar("Survivor Shooter", "Tạo prefab nhân vật...", 0.4f);
            var gunFx = LoadPrefab("Assets/Prefabs/GunParticles.prefab");
            var hitFx = LoadPrefab("Assets/Prefabs/HitParticles.prefab");
            var playerPrefab = BuildPlayerPrefab(playerCtrl, gunFx);
            var enemyPrefabs = new Dictionary<string, GameObject>();
            foreach (var e in Enemies)
                enemyPrefabs[e.name] = BuildEnemyPrefab(e, enemyCtrls[e.name], hitFx, shootableLayer);
            AssetDatabase.SaveAssets();

            EditorUtility.DisplayProgressBar("Survivor Shooter", "Dựng scene...", 0.6f);
            BuildScene(playerPrefab, enemyPrefabs, floorLayer);

            AssetDatabase.SaveAssets();
            EditorUtility.ClearProgressBar();
            EditorUtility.DisplayDialog("Survivor Shooter",
                "Hoàn tất! Scene đã mở sẵn: " + ScenePath + "\n\nNhấn Play để chơi.\nĐiều khiển: WASD di chuyển, chuột để ngắm, chuột trái để bắn.", "OK");
        }
        catch (Exception ex)
        {
            EditorUtility.ClearProgressBar();
            Debug.LogError("[SurvivorGameBuilder] Lỗi: " + ex);
            EditorUtility.DisplayDialog("Survivor Shooter", "Build thất bại:\n" + ex.Message + "\n\nXem Console để biết chi tiết.", "OK");
        }
    }

    // ------------------------------------------------------------------ layers

    static int EnsureLayer(string layerName)
    {
        int idx = LayerMask.NameToLayer(layerName);
        if (idx >= 0) return idx;

        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            var p = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(p.stringValue))
            {
                p.stringValue = layerName;
                tagManager.ApplyModifiedProperties();
                return i;
            }
        }
        throw new Exception("Không còn layer trống cho '" + layerName + "'.");
    }

    // --------------------------------------------------------------- animation

    static Dictionary<string, AnimationClip> LoadClips(string fbxPath)
    {
        return AssetDatabase.LoadAllAssetsAtPath(fbxPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .GroupBy(c => c.name)
            .ToDictionary(g => g.Key, g => g.First());
    }

    static AnimationClip Pick(Dictionary<string, AnimationClip> clips, string key, AnimationClip fallback)
    {
        AnimationClip c;
        return clips.TryGetValue(key, out c) ? c : fallback;
    }

    static void Link(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, string param)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.05f;
        t.AddCondition(mode, 0, param);
    }

    static void LinkAny(AnimatorStateMachine sm, AnimatorState to, string trigger)
    {
        var t = sm.AddAnyStateTransition(to);
        t.hasExitTime = false;
        t.duration = 0f;
        t.canTransitionToSelf = false;
        t.AddCondition(AnimatorConditionMode.If, 0, trigger);
    }

    static AnimatorController BuildPlayerController(Dictionary<string, AnimationClip> clips)
    {
        string path = AnimDir + "/PlayerAnimator.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);

        var sm = ctrl.layers[0].stateMachine;
        var idle = sm.AddState("Idle");  idle.motion = Pick(clips, "Idle", null);
        var move = sm.AddState("Move");  move.motion = Pick(clips, "Move", null);
        var death = sm.AddState("Death"); death.motion = Pick(clips, "Death", null);
        sm.defaultState = idle;

        Link(idle, move, AnimatorConditionMode.If, "IsWalking");
        Link(move, idle, AnimatorConditionMode.IfNot, "IsWalking");
        LinkAny(sm, death, "Die");
        return ctrl;
    }

    static AnimatorController BuildEnemyController(string enemyName, Dictionary<string, AnimationClip> clips)
    {
        string path = AnimDir + "/" + enemyName + "Animator.controller";
        AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("Dead", AnimatorControllerParameterType.Trigger);
        ctrl.AddParameter("PlayerDead", AnimatorControllerParameterType.Trigger);

        // Một số model (vd. ZomBear) không có clip tên Move/Idle -> dùng clip bất kỳ làm dự phòng.
        AnimationClip any = clips.Values.FirstOrDefault();

        var sm = ctrl.layers[0].stateMachine;
        var move = sm.AddState("Move");   move.motion = Pick(clips, "Move", any);
        var idle = sm.AddState("Idle");   idle.motion = Pick(clips, "Idle", any);
        var death = sm.AddState("Death"); death.motion = Pick(clips, "Death", null);
        sm.defaultState = move;

        Link(move, idle, AnimatorConditionMode.If, "PlayerDead");
        LinkAny(sm, death, "Dead");
        return ctrl;
    }

    // ----------------------------------------------------------------- prefabs

    static GameObject LoadPrefab(string path)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null) throw new Exception("Không tìm thấy asset: " + path);
        return go;
    }

    static AudioClip LoadSfx(string file)
    {
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Effects/" + file);
        if (c == null) Debug.LogWarning("Thiếu âm thanh: " + file);
        return c;
    }

    static Bounds GetBounds(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(go.transform.position + Vector3.up, new Vector3(1f, 2f, 1f));
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b;
    }

    static Transform FindDeep(Transform root, string childName)
    {
        if (root.name == childName) return root;
        foreach (Transform c in root)
        {
            var r = FindDeep(c, childName);
            if (r != null) return r;
        }
        return null;
    }

    static Animator EnsureAnimator(GameObject go, RuntimeAnimatorController ctrl)
    {
        var anim = go.GetComponent<Animator>();
        if (!anim) anim = go.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        return anim;
    }

    static GameObject SavePrefab(GameObject instance, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    static GameObject BuildPlayerPrefab(RuntimeAnimatorController ctrl, GameObject gunFx)
    {
        var model = LoadPrefab("Assets/Models/Characters/Player.fbx");
        var go = Object.Instantiate(model);
        go.name = "Player";
        go.tag = "Player";
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;

        float h = Mathf.Clamp(GetBounds(go).size.y, 1f, 3f);

        EnsureAnimator(go, ctrl);

        var rb = go.AddComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        var cap = go.AddComponent<CapsuleCollider>();
        cap.height = h;
        cap.radius = 0.3f;
        cap.center = new Vector3(0f, h * 0.5f, 0f);

        var audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.clip = LoadSfx("Player Hurt.wav");

        go.AddComponent<PlayerMovement>();
        var health = go.AddComponent<PlayerHealth>();
        health.deathClip = LoadSfx("Player Death.wav");

        // --- súng: gắn hiệu ứng + PlayerShooting vào đầu nòng ---
        Transform barrel = FindDeep(go.transform, "GunBarrelEnd");
        if (barrel == null)
        {
            Debug.LogWarning("Không tìm thấy GunBarrelEnd, tạo thủ công.");
            var b = new GameObject("GunBarrelEnd");
            b.transform.SetParent(go.transform, false);
            b.transform.localPosition = new Vector3(0f, 1f, 0.6f);
            barrel = b.transform;
        }

        var fx = (GameObject)PrefabUtility.InstantiatePrefab(gunFx);
        fx.transform.SetParent(barrel, false);
        fx.transform.localPosition = Vector3.zero;
        fx.transform.localRotation = Quaternion.identity;

        var line = barrel.gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/LineRenderMaterial.mat");
        line.startWidth = 0.05f;
        line.endWidth = 0.05f;
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;

        var light = barrel.gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.75f, 0.3f);
        light.intensity = 2f;
        light.range = 3f;
        light.shadows = LightShadows.None;
        light.enabled = false;

        var gunAudio = barrel.gameObject.AddComponent<AudioSource>();
        gunAudio.playOnAwake = false;
        gunAudio.clip = LoadSfx("Player GunShot.wav");

        barrel.gameObject.AddComponent<PlayerShooting>();

        return SavePrefab(go, CharPrefabDir + "/Player.prefab");
    }

    static GameObject BuildEnemyPrefab(EnemySpec s, RuntimeAnimatorController ctrl, GameObject hitFx, int shootableLayer)
    {
        var model = LoadPrefab("Assets/Models/Characters/" + s.fbx);
        var go = Object.Instantiate(model);
        go.name = s.name;
        go.layer = shootableLayer;
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;

        float h = Mathf.Clamp(GetBounds(go).size.y, 0.8f, 4f);

        EnsureAnimator(go, ctrl);

        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        var cap = go.AddComponent<CapsuleCollider>();
        cap.height = h;
        cap.radius = s.capsuleRadius;
        cap.center = new Vector3(0f, h * 0.5f, 0f);

        var trigger = go.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = s.attackRadius;
        trigger.center = new Vector3(0f, h * 0.5f, 0f);

        var agent = go.AddComponent<NavMeshAgent>();
        agent.speed = s.speed;
        agent.acceleration = 8f;
        agent.angularSpeed = 120f;
        agent.stoppingDistance = s.stopDistance;
        agent.radius = s.capsuleRadius;
        agent.height = h;

        var audio = go.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        audio.clip = LoadSfx(s.hurt);

        go.AddComponent<EnemyMovement>();
        var atk = go.AddComponent<EnemyAttack>();
        atk.attackDamage = s.damage;
        var hp = go.AddComponent<EnemyHealth>();
        hp.startingHealth = s.hp;
        hp.scoreValue = s.score;
        hp.deathClip = LoadSfx(s.death);

        var fx = (GameObject)PrefabUtility.InstantiatePrefab(hitFx);
        fx.transform.SetParent(go.transform, false);
        fx.transform.localPosition = Vector3.zero;
        fx.transform.localRotation = Quaternion.identity;

        return SavePrefab(go, CharPrefabDir + "/" + s.name + ".prefab");
    }

    // ------------------------------------------------------------------- scene

    static void BuildScene(GameObject playerPrefab, Dictionary<string, GameObject> enemyPrefabs, int floorLayer)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);

        // Ánh sáng & môi trường
        PrefabUtility.InstantiatePrefab(LoadPrefab("Assets/Prefabs/Lights.prefab"));
        var env = (GameObject)PrefabUtility.InstantiatePrefab(LoadPrefab("Assets/Prefabs/Environment.prefab"));
        env.transform.position = Vector3.zero;

        // Mặt phẳng vô hình để raycast chuột (layer Floor)
        var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
        floor.name = "FloorQuad";
        floor.layer = floorLayer;
        floor.transform.position = Vector3.zero;
        floor.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        floor.transform.localScale = new Vector3(100f, 100f, 1f);
        Object.DestroyImmediate(floor.GetComponent<MeshRenderer>());
        Object.DestroyImmediate(floor.GetComponent<MeshFilter>());

        // Bake NavMesh trước khi thêm nhân vật
        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        bool navOk = BakeNavMesh(env);

        // Player
        var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
        player.transform.position = Vector3.zero;
        var playerHealth = player.GetComponent<PlayerHealth>();

        // Camera
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        camGO.AddComponent<AudioListener>();
        cam.orthographic = true;
        cam.orthographicSize = 4.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGO.transform.position = new Vector3(1f, 15f, -22f);
        camGO.transform.rotation = Quaternion.Euler(30f, 0f, 0f);
        var follow = camGO.AddComponent<CameraFollow>();
        follow.target = player.transform;

        // Spawn points
        var spawnRoot = new GameObject("SpawnPoints");
        var spawns = new List<Transform>();
        Vector3[] candidates =
        {
            new Vector3(-20f, 0f, 12f), new Vector3(-20f, 0f, -12f),
            new Vector3(20f, 0f, 12f),  new Vector3(20f, 0f, -12f),
        };
        for (int i = 0; i < candidates.Length; i++)
        {
            Vector3 p = candidates[i];
            NavMeshHit hit;
            if (navOk && NavMesh.SamplePosition(p, out hit, 20f, NavMesh.AllAreas)) p = hit.position;
            var sp = new GameObject("SpawnPoint" + (i + 1));
            sp.transform.SetParent(spawnRoot.transform, false);
            sp.transform.position = p;
            spawns.Add(sp.transform);
        }

        // Enemy managers
        var managers = new GameObject("EnemyManagers");
        foreach (var e in Enemies)
        {
            var m = new GameObject(e.name + "Manager");
            m.transform.SetParent(managers.transform, false);
            var mgr = m.AddComponent<EnemyManager>();
            mgr.playerHealth = playerHealth;
            mgr.enemy = enemyPrefabs[e.name];
            mgr.spawnTime = e.spawnTime;
            mgr.minSpawnTime = e.minSpawnTime;
            mgr.rampPerSpawn = e.ramp;
            mgr.spawnPoints = spawns.ToArray();
        }

        // Nhạc nền
        var music = new GameObject("BackgroundMusic");
        var src = music.AddComponent<AudioSource>();
        src.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/Background Music.mp3");
        src.loop = true;
        src.playOnAwake = true;
        src.volume = 0.4f;
        src.spatialBlend = 0f;

        BuildHUD(playerHealth);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Selection.activeGameObject = player;
    }

    // ---------------------------------------------------------------- navmesh

    static bool BakeNavMesh(GameObject env)
    {
#if UNITY_2022_2_OR_NEWER
        // Unity 2022.2+ dùng package "AI Navigation" (NavMeshSurface).
        Type surfaceType = Type.GetType("Unity.AI.Navigation.NavMeshSurface, Unity.AI.Navigation");
        if (surfaceType == null)
        {
            Debug.LogError("[SurvivorGameBuilder] Chưa có package 'AI Navigation'. Mở Window > Package Manager > Unity Registry, " +
                           "cài 'AI Navigation', rồi chạy lại Tools > Survivor Shooter > Build Game.");
            EditorUtility.DisplayDialog("Survivor Shooter",
                "Chưa cài package 'AI Navigation' (cần để kẻ địch tìm đường).\n\nWindow > Package Manager > Unity Registry > AI Navigation > Install, rồi chạy lại menu Build Game.", "OK");
            return false;
        }

        var surfaceGO = new GameObject("NavMeshSurface");
        var surface = surfaceGO.AddComponent(surfaceType);
        surfaceType.GetMethod("BuildNavMesh").Invoke(surface, null);

        var data = surfaceType.GetProperty("navMeshData").GetValue(surface, null) as Object;
        if (data != null)
        {
            string dataPath = "Assets/Scenes/Main_NavMeshData.asset";
            AssetDatabase.DeleteAsset(dataPath);
            AssetDatabase.CreateAsset(data, dataPath);
        }
#else
        foreach (var t in env.GetComponentsInChildren<Transform>(true))
        {
            var flags = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags | StaticEditorFlags.NavigationStatic);
        }
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
#endif
        bool ok = NavMesh.CalculateTriangulation().vertices.Length > 0;
        if (!ok) Debug.LogWarning("[SurvivorGameBuilder] NavMesh rỗng - hãy bake lại thủ công (Window > AI > Navigation > Bake).");
        return ok;
    }

    // --------------------------------------------------------------------- UI

    static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static void SetRect(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = min; rt.anchorMax = max; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        SetRect(rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
    }

    static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
    {
        var rt = NewUI(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
        return img;
    }

    static Text NewText(string name, Transform parent, string content, int size, Color color, TextAnchor align, Font font)
    {
        var rt = NewUI(name, parent);
        rt.sizeDelta = new Vector2(700f, size * 1.4f);
        var t = rt.gameObject.AddComponent<Text>();
        t.text = content;
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        var o = rt.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.85f);
        o.effectDistance = new Vector2(2f, -2f);
        return t;
    }

    static Sprite Spr(string file)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/" + file);
        if (s == null) Debug.LogWarning("Thiếu sprite: " + file);
        return s;
    }

    static Font LoadFont()
    {
        var f = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/LuckiestGuy/LuckiestGuy.ttf");
        if (f != null) return f;
        f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    static void BuildHUD(PlayerHealth playerHealth)
    {
        Font font = LoadFont();

        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        var canvasGO = new GameObject("HUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        Transform root = canvasGO.transform;

        // Hiệu ứng đỏ khi trúng đòn
        var damage = NewImage("DamageImage", root, null, Color.clear);
        Stretch(damage.rectTransform);
        damage.raycastTarget = false;

        // Thanh máu
        var heart = NewImage("Heart", root, Spr("Heart.png"), Color.white);
        SetRect(heart.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(24f, 24f), new Vector2(48f, 48f));
        heart.preserveAspect = true;
        heart.raycastTarget = false;

        var sliderRT = NewUI("HealthSlider", root);
        SetRect(sliderRT, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(84f, 34f), new Vector2(300f, 28f));
        var slider = sliderRT.gameObject.AddComponent<Slider>();
        var bg = NewImage("Background", sliderRT, Spr("UISliderBG.png"), Color.white);
        Stretch(bg.rectTransform);
        bg.raycastTarget = false;
        var fillArea = NewUI("Fill Area", sliderRT);
        Stretch(fillArea);
        var fill = NewImage("Fill", fillArea, Spr("UISliderFill.png"), Color.white);
        fill.rectTransform.sizeDelta = Vector2.zero;
        fill.raycastTarget = false;
        slider.fillRect = fill.rectTransform;
        slider.direction = Slider.Direction.LeftToRight;
        slider.transition = Selectable.Transition.None;
        slider.interactable = false;
        slider.minValue = 0f;
        slider.maxValue = playerHealth.startingHealth;
        slider.wholeNumbers = true;
        slider.value = playerHealth.startingHealth;
        playerHealth.healthSlider = slider;
        playerHealth.damageImage = damage;

        // Điểm
        var score = NewText("ScoreText", root, "Score: 0", 44, Color.white, TextAnchor.MiddleCenter, font);
        SetRect(score.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(600f, 64f));
        score.gameObject.AddComponent<ScoreManager>();

        // Gợi ý điều khiển
        var hint = NewText("ControlsHint", root, "WASD: Move    Mouse: Aim    Left Click: Shoot", 22, new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleRight, font);
        SetRect(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 20f), new Vector2(700f, 32f));

        // Màn hình Game Over
        var panelImg = NewImage("GameOverPanel", root, null, new Color(0f, 0f, 0f, 0.65f));
        Stretch(panelImg.rectTransform);
        var group = panelImg.gameObject.AddComponent<CanvasGroup>();
        var gom = panelImg.gameObject.AddComponent<GameOverManager>();

        var box = NewImage("Box", panelImg.transform, Spr("UIPanel.png"), Color.white);
        SetRect(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 400f));
        box.raycastTarget = false;

        var title = NewText("Title", box.transform, "GAME OVER", 84, new Color(1f, 0.25f, 0.2f), TextAnchor.MiddleCenter, font);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(600f, 110f));
        var finalScore = NewText("FinalScore", box.transform, "Your Score: 0", 44, Color.white, TextAnchor.MiddleCenter, font);
        SetRect(finalScore.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(600f, 60f));
        var best = NewText("BestScore", box.transform, "Best: 0", 32, new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleCenter, font);
        SetRect(best.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(600f, 44f));

        var btnImg = NewImage("RestartButton", box.transform, Spr("UIButtonDefault.png"), Color.white);
        SetRect(btnImg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(240f, 64f));
        var btn = btnImg.gameObject.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.transition = Selectable.Transition.SpriteSwap;
        btn.spriteState = new SpriteState
        {
            highlightedSprite = Spr("UIButtonHighlighted.png"),
            pressedSprite = Spr("UIButtonPressed.png"),
        };
        UnityEventTools.AddPersistentListener(btn.onClick, playerHealth.RestartLevel);
        var btnText = NewText("Text", btnImg.transform, "RESTART", 34, Color.white, TextAnchor.MiddleCenter, font);
        Stretch(btnText.rectTransform);

        var restartHint = NewText("RestartHint", box.transform, "or press R", 20, new Color(1f, 1f, 1f, 0.7f), TextAnchor.MiddleCenter, font);
        SetRect(restartHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(400f, 28f));

        gom.playerHealth = playerHealth;
        gom.panel = group;
        gom.finalScoreText = finalScore;
        gom.bestScoreText = best;
    }
}
