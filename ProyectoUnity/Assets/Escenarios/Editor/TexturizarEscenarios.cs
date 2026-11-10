using UnityEditor;
using UnityEngine;

// Un solo menu para texturizar los tres escenarios ya hechos: Herramientas -> Texturizar sala, cochera y patio.
// (Los menus por separado Herramientas/Sala/..., Herramientas/Cochera/... y Herramientas/Patio/... siguen existiendo.)
public static class TexturizarEscenarios
{
    [MenuItem("Herramientas/Texturizar sala, cochera y patio")]
    public static void SalaCocheraYPatio()
    {
        SalaTexturizador.TexturizarTodo();
        CocheraTexturizador.TexturizarTodo();
        PatioTexturizador.TexturizarTodo();
        Debug.Log("[Escenarios] sala, cochera y patio texturizados");
    }
}
