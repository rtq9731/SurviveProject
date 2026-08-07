// Survive.Testing 안의 정적 클래스에서 인자 없는 IEnumerator 메서드를 전부 센다.
// 이름 규칙(FullRun 등)에 기대지 않는다 — 규칙을 벗어난 시나리오가 조용히 빠진다.
var sb = new System.Text.StringBuilder();
var asm = System.AppDomain.CurrentDomain.GetAssemblies()
    .FirstOrDefault(a => a.GetName().Name == "Survive.Runtime")
    ?? typeof(Survive.Testing.E2ERunner).Assembly;
var types = asm.GetTypes()
    .Where(t => t.Namespace == "Survive.Testing" && t.IsAbstract && t.IsSealed)
    .OrderBy(t => t.Name);
foreach (var t in types)
{
    foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                       .Where(m => m.ReturnType == typeof(System.Collections.IEnumerator) && m.GetParameters().Length == 0)
                       .OrderBy(m => m.Name))
        sb.Append(t.Name).Append('.').Append(m.Name).Append('\n');
}
return sb.ToString();
