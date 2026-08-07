// 공통 게이트 3번. 스윕을 돌리기 전후로 한 번씩 눌러 본다.
var r = Survive.EditorTools.AutonomousGate.Run();
return $"{{\"pass\":{r.pass.ToString().ToLower()},\"brokenReferences\":{r.brokenReferences},\"artViolations\":{r.artViolations},\"error\":\"{r.error}\",\"seconds\":{r.seconds}}}";
