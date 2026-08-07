// 에디터 창이 앞에 없어도 키 입력이 들어가게 한다.
Survive.EditorTools.E2EPlayModeInput.Enable();
var sc = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
return "focus=" + Survive.EditorTools.E2EPlayModeInput.IsEnabled + " scene=" + sc.name + " playing=" + UnityEditor.EditorApplication.isPlaying;
