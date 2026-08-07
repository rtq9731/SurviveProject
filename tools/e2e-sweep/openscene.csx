// 씬을 갈아 끼운다.
//
// 열 씬의 경로는 저장소 루트의 .sweep/scene.txt 에서 읽는다. execute-dynamic-code 에
// 인자를 넘길 창구가 없고, **환경변수도 못 쓴다** — 코드는 에디터 프로세스 안에서
// 도는데 셸에서 export 한 것은 그쪽 환경에 없다. 그래서 파일을 창구로 쓴다.
var root = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", ".."));
var file = System.IO.Path.Combine(root, ".sweep", "scene.txt");
if (!System.IO.File.Exists(file)) return "no scene.txt at " + file;

var path = System.IO.File.ReadAllText(file).Trim();
var sc = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
if (sc.path == path) return "already:" + sc.name;

var opened = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
return "opened:" + opened.name;
