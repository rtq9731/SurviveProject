// 재생 중인가 | 시나리오 상태 | 한 줄 요약
var st = Survive.Testing.E2ERunner.Status;
return UnityEditor.EditorApplication.isPlaying + "|" + st + "|" + Survive.Testing.E2ERunner.Summary();
