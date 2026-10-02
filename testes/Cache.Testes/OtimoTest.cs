using Xunit;

namespace Conde.Cache.Testes;

/// <summary>
/// O ótimo de Belady contra a busca exaustiva, e contra todas as outras
/// políticas.
/// </summary>
/// <remarks>
/// <para>
/// A regra de Belady é um teorema: descarte o item cujo próximo uso está mais
/// longe, e nenhuma política produzirá menos faltas. Só que um teorema aplicado a
/// código pode ter sido aplicado errado, e aí o juiz precisa ser outra coisa.
/// </para>
/// <para>
/// O juiz aqui é uma busca que não usa regra nenhuma: ela tenta <b>todas</b> as
/// escolhas de despejo possíveis, em todas as cadeias de até oito acessos sobre
/// quatro páginas, e fica com a melhor. Se a regra de Belady estivesse aplicada
/// errada, a diferença apareceria.
/// </para>
/// </remarks>
public class OtimoTest
{
    [Fact(DisplayName = "a regra de Belady contra a busca exaustiva, em 87.380 cadeias")]
    public void ContraABuscaExaustiva()
    {
        var cadeias = 0;

        foreach (var cadeia in Simulacao.TodasAsCadeias(4, 8))
        {
            for (var capacidade = 1; capacidade <= 4; capacidade++)
            {
                var pelaRegra = Simulacao.Rodar(new Otimo(capacidade, cadeia), cadeia);

                var pelaBusca = Otimo.MinimoPorForcaBruta(capacidade, cadeia);

                if (pelaRegra.Faltas != pelaBusca)
                {
                    Assert.Fail($"[{string.Join(",", cadeia)}] com {capacidade} quadros: "
                        + $"a regra deu {pelaRegra.Faltas} faltas e o minimo e {pelaBusca}");
                }
            }

            cadeias++;
        }

        // 4^0 + 4^1 + ... + 4^8.
        Assert.Equal(87_381, cadeias);
    }

    [Fact(DisplayName = "nenhuma politica bate o otimo, em cadeia nenhuma")]
    public void NinguemBateOOtimo()
    {
        // O ótimo é um PISO, e isso tem de valer em toda cadeia e todo tamanho.
        // Uma política que o batesse em um caso só seria prova de que a
        // implementação do ótimo está errada.
        var comparacoes = 0;

        foreach (var cadeia in Simulacao.TodasAsCadeias(4, 7))
        {
            for (var capacidade = 1; capacidade <= 4; capacidade++)
            {
                var otimo = Simulacao.Rodar(new Otimo(capacidade, cadeia), cadeia).Faltas;

                foreach (var politica in (IPolitica[])
                    [
                        new Lru(capacidade),
                        new Fifo(capacidade),
                        new Lfu(capacidade),
                        new SegundaChance(capacidade),
                    ])
                {
                    var faltas = Simulacao.Rodar(politica, cadeia).Faltas;

                    if (faltas < otimo)
                    {
                        Assert.Fail($"o {politica.Nome} fez {faltas} faltas e o otimo fez "
                            + $"{otimo} em [{string.Join(",", cadeia)}] com {capacidade} quadros");
                    }

                    comparacoes++;
                }
            }
        }

        Assert.True(comparacoes > 300_000, $"so {comparacoes} comparacoes");
    }

    [Fact(DisplayName = "o LRU rapido e o LRU devagar decidem igual, em toda cadeia")]
    public void OsDoisLru()
    {
        // Duas implementações da mesma política: uma com lista ligada mais
        // dicionário, em tempo constante, e outra que percorre uma lista a cada
        // acesso. Elas têm de concordar em todo acesso, e não só no total.
        var cadeias = 0;

        foreach (var cadeia in Simulacao.TodasAsCadeias(4, 8))
        {
            for (var capacidade = 1; capacidade <= 4; capacidade++)
            {
                var rapido = new Lru(capacidade);

                var devagar = new LruDevagar(capacidade);

                for (var passo = 0; passo < cadeia.Length; passo++)
                {
                    var umAcerto = rapido.Acessar(cadeia[passo], passo);

                    var outroAcerto = devagar.Acessar(cadeia[passo], passo);

                    if (umAcerto != outroAcerto)
                    {
                        Assert.Fail($"discordaram no passo {passo} de "
                            + $"[{string.Join(",", cadeia)}] com {capacidade} quadros");
                    }

                    // E o conteúdo do cache é o mesmo conjunto.
                    if (!rapido.Dentro.OrderBy(x => x).SequenceEqual(devagar.Dentro.OrderBy(x => x)))
                    {
                        Assert.Fail($"o conteudo divergiu no passo {passo} de "
                            + $"[{string.Join(",", cadeia)}]");
                    }
                }
            }

            cadeias++;
        }

        Assert.Equal(87_381, cadeias);
    }

    [Fact(DisplayName = "o cache nunca passa da capacidade, em politica nenhuma")]
    public void ACapacidadeEhRespeitada()
    {
        var sorteio = new Random(29);

        for (var caso = 0; caso < 2_000; caso++)
        {
            var paginas = sorteio.Next(1, 20);

            var capacidade = sorteio.Next(1, 10);

            var cadeia = Enumerable.Range(0, sorteio.Next(0, 100))
                .Select(_ => sorteio.Next(0, paginas))
                .ToArray();

            foreach (var politica in (IPolitica[])
                [
                    new Lru(capacidade),
                    new Fifo(capacidade),
                    new Lfu(capacidade),
                    new SegundaChance(capacidade),
                    new LruDevagar(capacidade),
                    new Otimo(capacidade, cadeia),
                ])
            {
                for (var passo = 0; passo < cadeia.Length; passo++)
                {
                    politica.Acessar(cadeia[passo], passo);

                    Assert.True(politica.Dentro.Count <= capacidade,
                        $"o {politica.Nome} guardou {politica.Dentro.Count} com capacidade {capacidade}");

                    // E o item recém-acessado está sempre lá.
                    Assert.Contains(cadeia[passo], politica.Dentro);

                    // Sem repetidos.
                    Assert.Equal(politica.Dentro.Count, politica.Dentro.Distinct().Count());
                }

                // Com menos páginas distintas que a capacidade, não há falta
                // depois da primeira volta.
                var distintas = cadeia.Distinct().Count();

                if (distintas <= capacidade)
                {
                    Assert.Equal(distintas, politica.Dentro.Count);
                }
            }
        }
    }

    [Fact(DisplayName = "as bordas: cadeia vazia, capacidade um, tudo igual")]
    public void AsBordas()
    {
        int[] vazia = [];

        foreach (var politica in (IPolitica[])
            [new Lru(3), new Fifo(3), new Lfu(3), new SegundaChance(3), new Otimo(3, vazia)])
        {
            var resultado = Simulacao.Rodar(politica, vazia);

            Assert.Equal(0, resultado.Acessos);
            Assert.Equal(0.0, resultado.Taxa);
            Assert.Empty(politica.Dentro);
        }

        // Capacidade um: todo acesso a uma página diferente da anterior é falta.
        int[] alternando = [1, 2, 1, 2, 1, 2];

        Assert.Equal(6, Simulacao.Rodar(new Lru(1), alternando).Faltas);
        Assert.Equal(6, Simulacao.Rodar(new Fifo(1), alternando).Faltas);

        // Com dois quadros, só as duas primeiras são falta.
        Assert.Equal(2, Simulacao.Rodar(new Lru(2), alternando).Faltas);

        // Sempre a mesma página: uma falta e o resto acerto.
        int[] mesma = [7, 7, 7, 7, 7];

        foreach (var politica in (IPolitica[])
            [new Lru(3), new Fifo(3), new Lfu(3), new SegundaChance(3), new Otimo(3, mesma)])
        {
            var resultado = Simulacao.Rodar(politica, mesma);

            Assert.Equal(1, resultado.Faltas);
            Assert.Equal(4, resultado.Acertos);
            Assert.Equal(0.8, resultado.Taxa);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => new Lru(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Fifo(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Lfu(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SegundaChance(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LruDevagar(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Otimo(0, vazia));
        Assert.Throws<ArgumentNullException>(() => new Otimo(1, null!));
        Assert.Throws<ArgumentNullException>(() => Simulacao.Rodar(null!, vazia));
        Assert.Throws<ArgumentNullException>(() => Simulacao.Rodar(new Lru(1), null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => Otimo.MinimoPorForcaBruta(0, vazia));
        Assert.Throws<ArgumentNullException>(() => Otimo.MinimoPorForcaBruta(1, null!));
    }
}
