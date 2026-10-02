using Xunit;

namespace Conde.Cache.Testes;

/// <summary>
/// A anomalia de Belady: aumentar o cache e piorar.
/// </summary>
/// <remarks>
/// <para>
/// A intuição diz que um cache maior nunca pode ser pior. A intuição está errada,
/// e Belady mostrou isso em 1969 com uma cadeia de referências em que o FIFO faz
/// <b>mais</b> faltas com quatro quadros do que com três.
/// </para>
/// <para>
/// Aqui esse fato não é citado, é procurado. O teste varre todas as cadeias por
/// tamanho crescente até achar a menor em que a anomalia acontece, e com isso a
/// afirmação deixa de ser folclore: ela vira um número, um exemplo concreto, e um
/// teste de regressão.
/// </para>
/// </remarks>
public class AnomaliaTest
{
    /// <summary>A menor cadeia em que a política piora ao ganhar um quadro.</summary>
    private static (int[] Cadeia, int Menor, int Maior, int FaltasMenor, int FaltasMaior)?
        ProcurarAnomalia(Func<int, IPolitica> fabrica, int paginas, int ate)
    {
        foreach (var cadeia in Simulacao.CadeiasCanonicas(paginas, ate))
        {
            for (var capacidade = 1; capacidade < paginas; capacidade++)
            {
                var comMenos = Simulacao.Rodar(fabrica(capacidade), cadeia).Faltas;

                var comMais = Simulacao.Rodar(fabrica(capacidade + 1), cadeia).Faltas;

                if (comMais > comMenos)
                {
                    return (cadeia, capacidade, capacidade + 1, comMenos, comMais);
                }
            }
        }

        return null;
    }

    [Fact(DisplayName = "a menor anomalia do FIFO e a cadeia de Belady, de 1969")]
    public void AAnomaliaDoFifo()
    {
        // A varredura e pelas cadeias canonicas, por tamanho crescente. Com ate
        // cinco paginas nao existe anomalia do FIFO com menos de doze acessos, e
        // a primeira que aparece com doze e a do artigo de Belady, Nelson e
        // Shedler (1969), so com as paginas renomeadas: 1,2,3,4,5 viram 0,1,2,3,4.
        //
        // Eu tinha escrito "nove acessos" aqui antes de medir. Errado.
        var achado = ProcurarAnomalia(capacidade => new Fifo(capacidade), 5, 12);

        Assert.NotNull(achado);

        var (cadeia, menor, maior, faltasMenor, faltasMaior) = achado.Value;

        Assert.Equal([0, 1, 2, 3, 0, 1, 4, 0, 1, 2, 3, 4], cadeia);
        Assert.Equal(3, menor);
        Assert.Equal(4, maior);
        Assert.Equal(9, faltasMenor);
        Assert.Equal(10, faltasMaior);

        // E a cadeia original, com os nomes do artigo.
        int[] doBelady = [1, 2, 3, 4, 1, 2, 5, 1, 2, 3, 4, 5];

        Assert.Equal(9, Simulacao.Rodar(new Fifo(3), doBelady).Faltas);
        Assert.Equal(10, Simulacao.Rodar(new Fifo(4), doBelady).Faltas);
    }

    [Fact(DisplayName = "renomear paginas nao muda as faltas, o que justifica a varredura canonica")]
    public void RenomearNaoMuda()
    {
        // A varredura canonica so vale se nenhuma politica olhar o VALOR da
        // pagina. Isso e conferido aqui: cada cadeia sorteada roda de novo com
        // as paginas embaralhadas por uma permutacao, e as faltas tem de bater.
        var sorteio = new Random(41);

        for (var caso = 0; caso < 2_000; caso++)
        {
            var cadeia = Enumerable.Range(0, sorteio.Next(1, 30)).Select(_ => sorteio.Next(0, 7)).ToArray();

            var permutacao = Enumerable.Range(0, 7).OrderBy(_ => sorteio.Next()).ToArray();

            var renomeada = cadeia.Select(p => permutacao[p] * 13 + 5).ToArray();

            for (var capacidade = 1; capacidade <= 5; capacidade++)
            {
                foreach (var fabrica in new Func<int, IPolitica>[] { c => new Lru(c), c => new Fifo(c), c => new Lfu(c), c => new SegundaChance(c) })
                {
                    Assert.Equal(Simulacao.Rodar(fabrica(capacidade), cadeia).Faltas,
                        Simulacao.Rodar(fabrica(capacidade), renomeada).Faltas);
                }

                Assert.Equal(Simulacao.Rodar(new Otimo(capacidade, cadeia), cadeia).Faltas,
                    Simulacao.Rodar(new Otimo(capacidade, renomeada), renomeada).Faltas);
            }
        }
    }

    [Fact(DisplayName = "as cadeias canonicas: uma por classe, e a contagem de Stirling")]
    public void AsCanonicas()
    {
        // Contagem conferida contra a soma de p^n e contra a definicao: cada
        // cadeia canonica abre as paginas em ordem 0, 1, 2...
        var contagem = 0;

        foreach (var cadeia in Simulacao.CadeiasCanonicas(4, 8))
        {
            var proxima = 0;

            foreach (var pagina in cadeia)
            {
                Assert.True(pagina <= proxima, $"[{string.Join(",", cadeia)}] nao e canonica");

                proxima = Math.Max(proxima, pagina + 1);
            }

            contagem++;
        }

        Assert.Equal(3_772, contagem);

        // E cada cadeia qualquer cai em exatamente uma canonica.
        var canonicas = Simulacao.CadeiasCanonicas(3, 6).Select(c => string.Join(",", c)).ToHashSet();

        foreach (var cadeia in Simulacao.TodasAsCadeias(3, 6))
        {
            var nomes = new Dictionary<int, int>();

            var forma = string.Join(",", cadeia.Select(p => nomes.TryGetValue(p, out var n) ? n : nomes[p] = nomes.Count));

            Assert.Contains(forma, canonicas);
        }
    }

    [Fact(DisplayName = "o LRU NUNCA piora ao ganhar um quadro, e isso e varrido")]
    public void OLruNaoTemAnomalia()
    {
        // O LRU tem a propriedade de pilha: o conteudo do cache de tamanho k e
        // sempre um subconjunto do de tamanho k+1. Dai sai que as faltas nunca
        // sobem, e isso e um teorema.
        //
        // O teste nao confia no teorema: ele varre todas as cadeias de ate nove
        // acessos sobre quatro paginas e exige a desigualdade em todas.
        var conferidas = 0;

        foreach (var cadeia in Simulacao.TodasAsCadeias(4, 9))
        {
            var anterior = int.MaxValue;

            for (var capacidade = 1; capacidade <= 5; capacidade++)
            {
                var faltas = Simulacao.Rodar(new Lru(capacidade), cadeia).Faltas;

                if (faltas > anterior)
                {
                    Assert.Fail($"o LRU piorou em [{string.Join(",", cadeia)}]: "
                        + $"{anterior} faltas com {capacidade - 1} quadros e {faltas} com "
                        + $"{capacidade}");
                }

                anterior = faltas;
            }

            conferidas++;
        }

        // 4^0 + 4^1 + ... + 4^9.
        Assert.Equal(349_525, conferidas);
    }

    [Fact(DisplayName = "a propriedade de pilha: o conteudo menor cabe dentro do maior")]
    public void APropriedadeDePilha()
    {
        // A razao de o LRU nao ter a anomalia, conferida diretamente: depois de
        // cada acesso, o conteudo do cache de tamanho k esta contido no de
        // tamanho k+1. O FIFO nao tem essa propriedade, e e por isso que ele
        // piora.
        var sorteio = new Random(29);

        var quebrasDoFifo = 0;

        var passosDoFifo = 0;

        for (var caso = 0; caso < 3_000; caso++)
        {
            var cadeia = Enumerable.Range(0, sorteio.Next(1, 40))
                .Select(_ => sorteio.Next(0, 6))
                .ToArray();

            for (var capacidade = 1; capacidade <= 5; capacidade++)
            {
                var menorLru = new Lru(capacidade);
                var maiorLru = new Lru(capacidade + 1);

                var menorFifo = new Fifo(capacidade);
                var maiorFifo = new Fifo(capacidade + 1);

                for (var passo = 0; passo < cadeia.Length; passo++)
                {
                    menorLru.Acessar(cadeia[passo], passo);
                    maiorLru.Acessar(cadeia[passo], passo);

                    menorFifo.Acessar(cadeia[passo], passo);
                    maiorFifo.Acessar(cadeia[passo], passo);

                    // O LRU sempre cabe.
                    if (!menorLru.Dentro.All(maiorLru.Dentro.Contains))
                    {
                        Assert.Fail($"o LRU quebrou a propriedade de pilha no passo {passo} "
                            + $"de [{string.Join(",", cadeia)}]");
                    }

                    // O FIFO, não, e o teste conta quantas vezes.
                    passosDoFifo++;

                    if (!menorFifo.Dentro.All(maiorFifo.Dentro.Contains))
                    {
                        quebrasDoFifo++;
                    }
                }
            }
        }

        Assert.True(quebrasDoFifo > 0,
            "o FIFO nao quebrou a propriedade de pilha nenhuma vez; se ele a tivesse, "
            + "a anomalia de Belady nao existiria");

        // A quebra não é rara: acontece numa fração grande dos passos.
        Assert.True(quebrasDoFifo > passosDoFifo / 20,
            $"o FIFO quebrou so {quebrasDoFifo} vezes em {passosDoFifo} passos");
    }

    [Fact(DisplayName = "a segunda chance piora mais cedo que o FIFO, e o LFU tambem tem")]
    public void AsOutrasPoliticas()
    {
        // A expectativa era que a segunda chance herdasse a anomalia do FIFO no
        // mesmo tamanho. Ela aparece MUITO antes: sete acessos. O bit de uso
        // faz a fila girar, e com dois quadros a pagina 0 ganha uma segunda vida
        // que com tres quadros ela nao precisa, e isso troca quem sai.
        var daSegundaChance = ProcurarAnomalia(capacidade => new SegundaChance(capacidade), 5, 11);

        Assert.NotNull(daSegundaChance);

        Assert.Equal([0, 0, 1, 1, 2, 3, 2], daSegundaChance.Value.Cadeia);
        Assert.Equal((4, 5), (daSegundaChance.Value.FaltasMenor, daSegundaChance.Value.FaltasMaior));

        // O LFU nao e baseado em ordem de chegada, e ainda assim nao tem a
        // propriedade de pilha. A menor tem dez acessos.
        var doLfu = ProcurarAnomalia(capacidade => new Lfu(capacidade), 5, 11);

        Assert.NotNull(doLfu);

        Assert.Equal([0, 0, 1, 2, 1, 2, 3, 0, 3, 0], doLfu.Value.Cadeia);
        Assert.Equal((6, 7), (doLfu.Value.FaltasMenor, doLfu.Value.FaltasMaior));

        // E o otimo nao tem: ele e uma politica de pilha, e isso tambem e um
        // teorema. Aqui a varredura e menor porque o otimo precisa da cadeia
        // inteira na mao.
        foreach (var cadeia in Simulacao.TodasAsCadeias(4, 8))
        {
            var anterior = int.MaxValue;

            for (var capacidade = 1; capacidade <= 5; capacidade++)
            {
                var faltas = Simulacao.Rodar(new Otimo(capacidade, cadeia), cadeia).Faltas;

                if (faltas > anterior)
                {
                    Assert.Fail($"o otimo piorou em [{string.Join(",", cadeia)}] ao ir "
                        + $"de {capacidade - 1} para {capacidade} quadros");
                }

                anterior = faltas;
            }
        }
    }
}
