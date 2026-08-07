// 실패한 판의 로그 꼬리. 왜 빨간지는 요약 한 줄로는 안 갈린다.
var s = Survive.Testing.E2EHarness.LogBuffer.ToString();
var lines = s.Replace("\r\n", "\n").Split('\n');
int n = System.Math.Min(14, lines.Length);
return string.Join(" // ", lines.Skip(System.Math.Max(0, lines.Length - n)).Where(x => x.Trim().Length > 0));
