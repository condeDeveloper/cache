namespace Conde.Cache;

/// <summary>
/// O ótimo de Belady: a política que não dá para bater.
/// </summary>
/// <remarks>
/// <para>
/// A regra é uma linha: descarte o item cujo próximo uso está mais longe no
/// futuro. Belady provou em 1966 que nenhuma política produz menos faltas, e a
/// prova é por troca: qualquer escolha diferente pode ser transformada nesta sem
/// aumentar o número de faltas.
/// </para>
/// <para>
/// Ela é impossível de usar, porque precisa conhecer o futuro. E é exatamente por
/// isso que ela é valiosa aqui: é o <b>piso</b> contra o qual qualquer política de
/// verdade pode ser medida. Sem ele, dizer que o LRU "vai bem" é opinião; com ele,
/// é a distância até um número que ninguém pode melhorar.
/// </para>
/// </remarks>
public sealed class Otimo : IPolitica
{
    private readonly int[] _proximo;

    private readonly Dictionary<int, int> _dentro = [];

    private readonly int _capacidade;

    /// <summary>Monta a política com a cadeia inteira à vista.</summary>
    /// <remarks>
    /// O próximo uso de cada posição é calculado uma vez, de trás para a frente.
    /// A primeira versão procurava o próximo uso a cada despejo, varrendo o
    /// futuro, e com vinte mil acessos e cem quadros isso passava de dez bilhões
    /// de comparações. Com a tabela, cada despejo custa uma passada pelo cache.
    /// </remarks>
    public Otimo(int capacidade, IReadOnlyList<int> cadeia)
    {
        ArgumentNullException.ThrowIfNull(cadeia);

        if (capacidade <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacidade), capacidade, "é pelo menos 1");
        }

        _capacidade = capacidade;

        _proximo = new int[cadeia.Count];

        var visto = new Dictionary<int, int>();

        for (var i = cadeia.Count - 1; i >= 0; i--)
        {
            _proximo[i] = visto.TryGetValue(cadeia[i], out var depois) ? depois : int.MaxValue;

            visto[cadeia[i]] = i;
        }
    }

    /// <inheritdoc/>
    public string Nome => "otimo";

    /// <inheritdoc/>
    public IReadOnlyCollection<int> Dentro => _dentro.Keys;

    /// <inheritdoc/>
    public bool Acessar(int chave, int passo)
    {
        // O valor guardado de cada item é o próximo passo em que ele volta.
        var volta = passo < _proximo.Length ? _proximo[passo] : int.MaxValue;

        if (_dentro.ContainsKey(chave))
        {
            _dentro[chave] = volta;

            return true;
        }

        if (_dentro.Count == _capacidade)
        {
            var vitima = 0;

            var maisLonge = -1;

            foreach (var (candidato, proximo) in _dentro)
            {
                // Item que não volta nunca: sai na hora, e não há escolha melhor.
                if (proximo == int.MaxValue)
                {
                    vitima = candidato;

                    break;
                }

                if (proximo > maisLonge)
                {
                    maisLonge = proximo;

                    vitima = candidato;
                }
            }

            _dentro.Remove(vitima);
        }

        _dentro[chave] = volta;

        return false;
    }

    /// <summary>
    /// O mínimo de faltas possível, achado por busca exaustiva sobre todas as
    /// escolhas de despejo.
    /// </summary>
    /// <remarks>
    /// <para>
    /// É o juiz do juiz. A regra de Belady é um teorema, e um teorema aplicado a
    /// código pode ter sido aplicado errado; esta busca não usa regra nenhuma,
    /// ela tenta <b>todas</b> as escolhas possíveis e fica com a melhor.
    /// </para>
    /// <para>
    /// O custo é exponencial no número de despejos, então ela só serve para
    /// cadeias curtas. É o bastante: se a regra de Belady estivesse errada, ela
    /// estaria errada já em cadeias curtas.
    /// </para>
    /// </remarks>
    public static int MinimoPorForcaBruta(int capacidade, IReadOnlyList<int> cadeia)
    {
        ArgumentNullException.ThrowIfNull(cadeia);

        if (capacidade <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacidade), capacidade, "é pelo menos 1");
        }

        var memoria = new Dictionary<(int Passo, string Estado), int>();

        int Descer(int passo, SortedSet<int> dentro)
        {
            if (passo == cadeia.Count)
            {
                return 0;
            }

            var assinatura = (passo, string.Join(",", dentro));

            if (memoria.TryGetValue(assinatura, out var guardado))
            {
                return guardado;
            }

            var chave = cadeia[passo];

            int resposta;

            if (dentro.Contains(chave))
            {
                resposta = Descer(passo + 1, dentro);
            }
            else if (dentro.Count < capacidade)
            {
                dentro.Add(chave);

                resposta = 1 + Descer(passo + 1, dentro);

                dentro.Remove(chave);
            }
            else
            {
                resposta = int.MaxValue;

                // Tenta despejar CADA um dos que estão dentro.
                foreach (var vitima in dentro.ToArray())
                {
                    dentro.Remove(vitima);
                    dentro.Add(chave);

                    var daqui = Descer(passo + 1, dentro);

                    dentro.Remove(chave);
                    dentro.Add(vitima);

                    resposta = Math.Min(resposta, daqui);
                }

                resposta = resposta == int.MaxValue ? int.MaxValue : resposta + 1;
            }

            memoria[assinatura] = resposta;

            return resposta;
        }

        return Descer(0, []);
    }
}
