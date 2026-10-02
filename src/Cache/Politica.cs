namespace Conde.Cache;

/// <summary>
/// Uma política de substituição: quem sai quando o cache enche.
/// </summary>
/// <remarks>
/// A política recebe a sequência de acessos um a um e decide, quando precisa
/// abrir espaço, qual item descartar. É a única decisão que ela toma, e é ela que
/// separa um cache que funciona de um que não funciona.
/// </remarks>
public interface IPolitica
{
    /// <summary>O nome, para as tabelas.</summary>
    string Nome { get; }

    /// <summary>
    /// Processa um acesso e diz se ele foi acerto.
    /// </summary>
    /// <param name="chave">O item acessado.</param>
    /// <param name="passo">Em que posição da sequência estamos, contando de zero.</param>
    bool Acessar(int chave, int passo);

    /// <summary>Os itens que estão no cache agora.</summary>
    IReadOnlyCollection<int> Dentro { get; }
}

/// <summary>O que uma simulação devolve.</summary>
/// <param name="Acertos">Quantos acessos encontraram o item no cache.</param>
/// <param name="Faltas">Quantos não encontraram.</param>
public readonly record struct Resultado(int Acertos, int Faltas)
{
    /// <summary>Quantos acessos houve.</summary>
    public int Acessos => Acertos + Faltas;

    /// <summary>A fração de acertos.</summary>
    public double Taxa => Acessos == 0 ? 0 : (double)Acertos / Acessos;
}

/// <summary>
/// Roda uma sequência de acessos por uma política e conta.
/// </summary>
/// <remarks>
/// <para>
/// Separar a simulação da política é o que permite comparar todas elas contra a
/// mesma sequência, e é o que permite comparar qualquer uma contra o ótimo.
/// </para>
/// <para>
/// A sequência de acessos chama-se cadeia de referências na literatura, e é a
/// única entrada que importa: dado o tamanho do cache e a cadeia, o número de
/// faltas de cada política é determinado.
/// </para>
/// </remarks>
public static class Simulacao
{
    /// <summary>Roda a cadeia e conta acertos e faltas.</summary>
    public static Resultado Rodar(IPolitica politica, IReadOnlyList<int> cadeia)
    {
        ArgumentNullException.ThrowIfNull(politica);
        ArgumentNullException.ThrowIfNull(cadeia);

        var acertos = 0;

        for (var passo = 0; passo < cadeia.Count; passo++)
        {
            if (politica.Acessar(cadeia[passo], passo))
            {
                acertos++;
            }
        }

        return new Resultado(acertos, cadeia.Count - acertos);
    }

    /// <summary>
    /// Todas as cadeias de referências de um tamanho dado sobre um alfabeto dado.
    /// </summary>
    /// <remarks>
    /// <para>
    /// É o que torna possível varrer o espaço inteiro em vez de sortear. Com
    /// quatro páginas distintas e cadeias de até oito acessos, são 87.380 cadeias,
    /// e nenhuma delas é amostra.
    /// </para>
    /// <para>
    /// Em cache, o caso interessante quase nunca é o aleatório: é a cadeia
    /// arrumada de propósito, como a que faz a quantidade de faltas <b>subir</b>
    /// quando o cache cresce.
    /// </para>
    /// </remarks>
    public static IEnumerable<int[]> TodasAsCadeias(int paginas, int ate)
    {
        var cadeia = new int[ate];

        IEnumerable<int[]> Descer(int tamanho, int posicao)
        {
            if (posicao == tamanho)
            {
                yield return cadeia[..tamanho];

                yield break;
            }

            for (var pagina = 0; pagina < paginas; pagina++)
            {
                cadeia[posicao] = pagina;

                foreach (var pronta in Descer(tamanho, posicao + 1))
                {
                    yield return pronta;
                }
            }
        }

        for (var tamanho = 0; tamanho <= ate; tamanho++)
        {
            foreach (var pronta in Descer(tamanho, 0))
            {
                yield return pronta;
            }
        }
    }

    /// <summary>
    /// As cadeias canônicas: uma por classe de renomeação de páginas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nenhuma política daqui olha o <b>valor</b> da página, só a ordem em que
    /// ela aparece. Então [7, 3, 7] e [0, 1, 0] fazem as mesmas faltas em qualquer
    /// uma delas, e varrer as duas é trabalho jogado fora.
    /// </para>
    /// <para>
    /// A cadeia canônica nomeia as páginas pela ordem da primeira aparição: a
    /// primeira página nova é 0, a segunda é 1, e assim por diante. Cada classe
    /// tem exatamente uma, e o número delas cai de p<sup>n</sup> para a soma dos
    /// números de Stirling de segunda espécie. Com cinco páginas e até onze
    /// acessos, são 61.035.156 cadeias contra 531.457 canônicas: 115 vezes menos
    /// (medido com <c>medidor contar</c>).
    /// </para>
    /// </remarks>
    public static IEnumerable<int[]> CadeiasCanonicas(int paginas, int ate)
    {
        var cadeia = new int[ate];

        IEnumerable<int[]> Descer(int tamanho, int posicao, int usadas)
        {
            if (posicao == tamanho)
            {
                yield return cadeia[..tamanho];

                yield break;
            }

            // Pode repetir qualquer página já vista, ou abrir a próxima nova.
            var limite = Math.Min(usadas + 1, paginas);

            for (var pagina = 0; pagina < limite; pagina++)
            {
                cadeia[posicao] = pagina;

                foreach (var pronta in Descer(tamanho, posicao + 1, Math.Max(usadas, pagina + 1)))
                {
                    yield return pronta;
                }
            }
        }

        for (var tamanho = 0; tamanho <= ate; tamanho++)
        {
            foreach (var pronta in Descer(tamanho, 0, 0))
            {
                yield return pronta;
            }
        }
    }
}
