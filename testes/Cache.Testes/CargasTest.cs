using Xunit;

namespace Conde.Cache.Testes;

/// <summary>
/// As cargas sintéticas e o que cada uma faz com as políticas.
/// </summary>
public class CargasTest
{
    [Fact(DisplayName = "zipf: a pagina 0 aparece perto de 1/H(n) das vezes")]
    public void ZipfTemAFormaCerta()
    {
        // Com s = 1 e mil paginas, o peso da primeira e 1 / H(1000), e
        // H(1000) = 7,4855. Com duzentos mil sorteios o erro padrao e de 0,07
        // ponto percentual, entao a tolerancia de 0,5 ponto nao e frouxa.
        var cadeia = Cargas.Zipf(1_000, 1.0, 200_000, 3);

        var harmonico = Enumerable.Range(1, 1_000).Sum(k => 1.0 / k);

        var esperado = 1.0 / harmonico;

        var medido = cadeia.Count(p => p == 0) / (double)cadeia.Length;

        Assert.InRange(medido, esperado - 0.005, esperado + 0.005);

        // E a segunda pagina aparece metade das vezes da primeira.
        var razao = cadeia.Count(p => p == 1) / (double)cadeia.Count(p => p == 0);

        Assert.InRange(razao, 0.47, 0.53);
    }

    [Fact(DisplayName = "o laco um pouco maior que o cache zera o LRU e o FIFO")]
    public void OLacoZeraOLru()
    {
        var cadeia = Cargas.Laco(11, 1_100);

        Assert.Equal(0, Simulacao.Rodar(new Lru(10), cadeia).Acertos);
        Assert.Equal(0, Simulacao.Rodar(new Fifo(10), cadeia).Acertos);

        // O otimo acerta quase tudo: com dez quadros e um laco de onze, so uma
        // pagina precisa faltar por volta, depois do aquecimento.
        var otimo = Simulacao.Rodar(new Otimo(10, cadeia), cadeia);

        Assert.True(otimo.Taxa > 0.85, $"o otimo acertou {otimo.Taxa:P1}");
    }

    [Fact(DisplayName = "com varredura no meio, o LFU segura o conjunto quente e o LRU nao")]
    public void AVarreduraDerrubaOLru()
    {
        var cadeia = Cargas.VarreduraComQuente(1_000, 50, 20_000, 11);

        var lru = Simulacao.Rodar(new Lru(50), cadeia).Taxa;

        var lfu = Simulacao.Rodar(new Lfu(50), cadeia).Taxa;

        // Metade dos acessos e varredura que nunca se repete, entao 50% e o
        // teto de qualquer politica, e o otimo encosta nele.
        Assert.True(lfu > 2 * lru, $"LFU {lfu:P1} contra LRU {lru:P1}");

        Assert.True(Simulacao.Rodar(new Otimo(50, cadeia), cadeia).Taxa <= 0.5 + 1e-9);
    }

    [Fact(DisplayName = "as cargas sao deterministicas pela semente")]
    public void Determinismo()
    {
        Assert.Equal(Cargas.Zipf(500, 0.9, 1_000, 5), Cargas.Zipf(500, 0.9, 1_000, 5));

        Assert.NotEqual(Cargas.Zipf(500, 0.9, 1_000, 5), Cargas.Zipf(500, 0.9, 1_000, 6));

        Assert.Equal(Cargas.VarreduraComQuente(100, 10, 1_000, 2), Cargas.VarreduraComQuente(100, 10, 1_000, 2));
    }
}
