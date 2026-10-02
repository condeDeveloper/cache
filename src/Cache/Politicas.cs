namespace Conde.Cache;

/// <summary>
/// O menos usado recentemente, em tempo constante.
/// </summary>
/// <remarks>
/// <para>
/// A aposta do LRU é que o passado recente prevê o futuro próximo, e ela acerta
/// na maioria das cargas reais porque programas têm localidade temporal.
/// </para>
/// <para>
/// A estrutura é a combinação clássica: uma lista ligada que guarda a ordem de
/// uso e um dicionário que leva da chave direto para o nó da lista. Sem o
/// dicionário, achar o item na lista custaria percorrer; sem a lista, saber quem
/// é o mais antigo custaria percorrer.
/// </para>
/// </remarks>
public sealed class Lru(int capacidade) : IPolitica
{
    private readonly LinkedList<int> _ordem = new();

    private readonly Dictionary<int, LinkedListNode<int>> _onde = [];

    private readonly int _capacidade = capacidade > 0
        ? capacidade
        : throw new ArgumentOutOfRangeException(nameof(capacidade), capacidade, "é pelo menos 1");

    /// <inheritdoc/>
    public string Nome => "LRU";

    /// <inheritdoc/>
    public IReadOnlyCollection<int> Dentro => _onde.Keys;

    /// <inheritdoc/>
    public bool Acessar(int chave, int passo)
    {
        if (_onde.TryGetValue(chave, out var no))
        {
            // Já está: só volta para a frente da fila.
            _ordem.Remove(no);
            _ordem.AddFirst(no);

            return true;
        }

        if (_onde.Count == _capacidade)
        {
            var velho = _ordem.Last!;

            _ordem.RemoveLast();

            _onde.Remove(velho.Value);
        }

        _onde[chave] = _ordem.AddFirst(chave);

        return false;
    }
}

/// <summary>
/// O primeiro que entrou é o primeiro que sai.
/// </summary>
/// <remarks>
/// A política mais simples que existe, e a que tem o defeito mais famoso: com ela
/// aumentar o cache pode <b>aumentar</b> o número de faltas. Isso chama-se anomalia
/// de Belady, e um dos testes procura a menor cadeia em que ela acontece.
/// </remarks>
public sealed class Fifo(int capacidade) : IPolitica
{
    private readonly Queue<int> _fila = new();

    private readonly HashSet<int> _dentro = [];

    private readonly int _capacidade = capacidade > 0
        ? capacidade
        : throw new ArgumentOutOfRangeException(nameof(capacidade), capacidade, "é pelo menos 1");

    /// <inheritdoc/>
    public string Nome => "FIFO";

    /// <inheritdoc/>
    public IReadOnlyCollection<int> Dentro => _dentro;

    /// <inheritdoc/>
    public bool Acessar(int chave, int passo)
    {
        if (_dentro.Contains(chave))
        {
            // Acerto não mexe na ordem: é isso que separa o FIFO do LRU.
            return true;
        }

        if (_dentro.Count == _capacidade)
        {
            _dentro.Remove(_fila.Dequeue());
        }

        _fila.Enqueue(chave);

        _dentro.Add(chave);

        return false;
    }
}

/// <summary>
/// O menos usado em quantidade, com desempate pelo mais antigo.
/// </summary>
/// <remarks>
/// <para>
/// O LFU aposta na frequência em vez da recência, e o problema dele tem nome:
/// poluição por cache. Um item muito acessado no começo do dia fica com a
/// contagem alta e não sai mais, mesmo depois de ninguém mais querer ele.
/// </para>
/// <para>
/// O desempate precisa ser definido e quase nunca é. Aqui é o mais antigo a
/// entrar, o que torna a política determinística: sem isso, duas implementações
/// corretas dariam resultados diferentes e nenhum teste pegaria a diferença.
/// </para>
/// </remarks>
public sealed class Lfu(int capacidade) : IPolitica
{
    private readonly Dictionary<int, (int Vezes, int Quando)> _contagem = [];

    private readonly int _capacidade = capacidade > 0
        ? capacidade
        : throw new ArgumentOutOfRangeException(nameof(capacidade), capacidade, "é pelo menos 1");

    /// <inheritdoc/>
    public string Nome => "LFU";

    /// <inheritdoc/>
    public IReadOnlyCollection<int> Dentro => _contagem.Keys;

    /// <inheritdoc/>
    public bool Acessar(int chave, int passo)
    {
        if (_contagem.TryGetValue(chave, out var atual))
        {
            _contagem[chave] = (atual.Vezes + 1, atual.Quando);

            return true;
        }

        if (_contagem.Count == _capacidade)
        {
            var vitima = _contagem
                .OrderBy(p => p.Value.Vezes)
                .ThenBy(p => p.Value.Quando)
                .First()
                .Key;

            _contagem.Remove(vitima);
        }

        _contagem[chave] = (1, passo);

        return false;
    }
}

/// <summary>
/// A segunda chance, também chamada de relógio.
/// </summary>
/// <remarks>
/// <para>
/// É o FIFO com um bit por item. Quando o item acertaria, o bit liga; quando
/// chega a vez dele de sair, se o bit está ligado ele desliga e vai para o fim da
/// fila em vez de sair.
/// </para>
/// <para>
/// É a aproximação de LRU que os sistemas operacionais de fato usam, porque
/// manter a ordem exata de uso exigiria atualizar uma lista a cada acesso à
/// memória, e isso é caro demais para o hardware fazer.
/// </para>
/// </remarks>
public sealed class SegundaChance(int capacidade) : IPolitica
{
    private readonly LinkedList<int> _fila = new();

    private readonly Dictionary<int, bool> _bit = [];

    private readonly int _capacidade = capacidade > 0
        ? capacidade
        : throw new ArgumentOutOfRangeException(nameof(capacidade), capacidade, "é pelo menos 1");

    /// <inheritdoc/>
    public string Nome => "2a chance";

    /// <inheritdoc/>
    public IReadOnlyCollection<int> Dentro => _bit.Keys;

    /// <inheritdoc/>
    public bool Acessar(int chave, int passo)
    {
        if (_bit.ContainsKey(chave))
        {
            _bit[chave] = true;

            return true;
        }

        if (_bit.Count == _capacidade)
        {
            while (true)
            {
                var candidato = _fila.First!.Value;

                _fila.RemoveFirst();

                if (_bit[candidato])
                {
                    // Tinha a segunda chance: perde o bit e volta para o fim.
                    _bit[candidato] = false;

                    _fila.AddLast(candidato);

                    continue;
                }

                _bit.Remove(candidato);

                break;
            }
        }

        _fila.AddLast(chave);

        _bit[chave] = false;

        return false;
    }
}

/// <summary>
/// O LRU escrito do jeito óbvio e lento, que serve de juiz.
/// </summary>
/// <remarks>
/// Uma lista de chaves em ordem de uso, percorrida a cada acesso. Custa
/// <c>O(capacidade)</c> por operação em vez de <c>O(1)</c>, e é impossível de
/// errar, que é o que se quer de uma referência.
/// </remarks>
public sealed class LruDevagar(int capacidade) : IPolitica
{
    private readonly List<int> _ordem = [];

    private readonly int _capacidade = capacidade > 0
        ? capacidade
        : throw new ArgumentOutOfRangeException(nameof(capacidade), capacidade, "é pelo menos 1");

    /// <inheritdoc/>
    public string Nome => "LRU devagar";

    /// <inheritdoc/>
    public IReadOnlyCollection<int> Dentro => _ordem;

    /// <inheritdoc/>
    public bool Acessar(int chave, int passo)
    {
        var onde = _ordem.IndexOf(chave);

        if (onde >= 0)
        {
            _ordem.RemoveAt(onde);
            _ordem.Insert(0, chave);

            return true;
        }

        if (_ordem.Count == _capacidade)
        {
            _ordem.RemoveAt(_ordem.Count - 1);
        }

        _ordem.Insert(0, chave);

        return false;
    }
}
