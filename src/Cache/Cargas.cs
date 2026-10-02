namespace Conde.Cache;

/// <summary>
/// Cadeias de referências sintéticas, cada uma com um formato que favorece ou
/// derruba uma política.
/// </summary>
/// <remarks>
/// Política de cache não tem nota absoluta: tem nota por carga. O LRU brilha
/// onde há localidade e é o pior possível num laço um pouco maior que o cache.
/// As três cargas daqui são escolhidas para mostrar isso, e todas são
/// determinísticas pela semente.
/// </remarks>
public static class Cargas
{
    /// <summary>
    /// Acessos com popularidade de Zipf: a página k aparece com peso 1/k^s.
    /// </summary>
    /// <remarks>
    /// É o formato de quase toda carga real de cache web: poucas páginas
    /// recebem quase tudo, e a cauda é longa. O sorteio é por busca binária na
    /// distribuição acumulada.
    /// </remarks>
    public static int[] Zipf(int paginas, double s, int acessos, int semente)
    {
        var acumulada = new double[paginas];

        var total = 0.0;

        for (var k = 0; k < paginas; k++)
        {
            total += 1.0 / Math.Pow(k + 1, s);

            acumulada[k] = total;
        }

        var sorteio = new Random(semente);

        var cadeia = new int[acessos];

        for (var i = 0; i < acessos; i++)
        {
            var alvo = sorteio.NextDouble() * total;

            var onde = Array.BinarySearch(acumulada, alvo);

            cadeia[i] = onde >= 0 ? onde : ~onde;
        }

        return cadeia;
    }

    /// <summary>Um laço que percorre as mesmas páginas em ordem, sem parar.</summary>
    /// <remarks>
    /// O pesadelo do LRU: se o laço é maior que o cache, cada página é despejada
    /// logo antes de voltar a ser usada, e a taxa de acerto é zero.
    /// </remarks>
    public static int[] Laco(int tamanho, int acessos)
    {
        var cadeia = new int[acessos];

        for (var i = 0; i < acessos; i++)
        {
            cadeia[i] = i % tamanho;
        }

        return cadeia;
    }

    /// <summary>
    /// Um conjunto quente usado o tempo todo, misturado com uma varredura longa
    /// que nunca se repete.
    /// </summary>
    /// <remarks>
    /// É o caso que motivou o LFU e as variantes com frequência: a varredura
    /// empurra o conjunto quente para fora de um LRU, e um contador de usos
    /// segura.
    /// </remarks>
    public static int[] VarreduraComQuente(int paginas, int quentes, int acessos, int semente)
    {
        var sorteio = new Random(semente);

        var cadeia = new int[acessos];

        var varredura = quentes;

        for (var i = 0; i < acessos; i++)
        {
            if (sorteio.NextDouble() < 0.5)
            {
                cadeia[i] = sorteio.Next(quentes);
            }
            else
            {
                cadeia[i] = varredura;

                varredura = varredura + 1 < paginas * 20 ? varredura + 1 : quentes;
            }
        }

        return cadeia;
    }
}
