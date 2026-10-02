using System.Diagnostics;
using System.Globalization;
using Conde.Cache;

// A ferramenta de medida. Cada comando imprime numeros que entram no README, e
// nenhum deles e citado sem ter saido daqui.
//
//   contar    quantas cadeias a varredura canonica economiza
//   anomalia  a menor cadeia em que cada politica piora ao ganhar um quadro
//   carga     taxa de acerto de cada politica em tres cargas, contra o otimo

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var comando = args.Length > 0 ? args[0] : "carga";

switch (comando)
{
    case "contar":
        Contar();
        break;

    case "anomalia":
        Anomalia();
        break;

    case "carga":
        Carga();
        break;

    default:
        Console.Error.WriteLine($"comando desconhecido: {comando} (use contar, anomalia ou carga)");
        return 1;
}

return 0;

static void Contar()
{
    Console.WriteLine("paginas  acessos  todas          canonicas    razao");

    foreach (var (paginas, ate) in new[] { (4, 8), (4, 9), (5, 9), (5, 10), (5, 11) })
    {
        long todas = 0;

        for (var n = 0; n <= ate; n++)
        {
            todas += (long)Math.Pow(paginas, n);
        }

        long canonicas = Simulacao.CadeiasCanonicas(paginas, ate).LongCount();

        Console.WriteLine($"{paginas,7}  {ate,7}  {todas,12:N0}  {canonicas,11:N0}  {(double)todas / canonicas,6:F1}x");
    }
}

static void Anomalia()
{
    var politicas = new (string Nome, Func<int, IPolitica> Fabrica)[]
    {
        ("FIFO", c => new Fifo(c)),
        ("2a chance", c => new SegundaChance(c)),
        ("LFU", c => new Lfu(c)),
        ("LRU", c => new Lru(c)),
    };

    foreach (var (nome, fabrica) in politicas)
    {
        var relogio = Stopwatch.StartNew();

        var achado = Procurar(fabrica, 5, nome == "FIFO" ? 12 : 11);

        relogio.Stop();

        if (achado is null)
        {
            Console.WriteLine($"{nome,-10} nenhuma anomalia ate 11 acessos com 5 paginas ({relogio.ElapsedMilliseconds} ms)");

            continue;
        }

        var (cadeia, menor, faltasMenor, faltasMaior) = achado.Value;

        Console.WriteLine($"{nome,-10} {cadeia.Length} acessos: [{string.Join(",", cadeia)}]  "
            + $"{menor} quadros = {faltasMenor} faltas, {menor + 1} quadros = {faltasMaior} faltas  "
            + $"({relogio.ElapsedMilliseconds} ms)");
    }
}

static (int[] Cadeia, int Menor, int FaltasMenor, int FaltasMaior)? Procurar(
    Func<int, IPolitica> fabrica, int paginas, int ate)
{
    foreach (var cadeia in Simulacao.CadeiasCanonicas(paginas, ate))
    {
        for (var capacidade = 1; capacidade < paginas; capacidade++)
        {
            var comMenos = Simulacao.Rodar(fabrica(capacidade), cadeia).Faltas;

            var comMais = Simulacao.Rodar(fabrica(capacidade + 1), cadeia).Faltas;

            if (comMais > comMenos)
            {
                return (cadeia, capacidade, comMenos, comMais);
            }
        }
    }

    return null;
}

static void Carga()
{
    const int Acessos = 20_000;

    const int Paginas = 1_000;

    var cargas = new (string Nome, int[] Cadeia)[]
    {
        ("zipf 1.0", Cargas.Zipf(Paginas, 1.0, Acessos, 7)),
        ("laco de 120", Cargas.Laco(120, Acessos)),
        ("varredura + quente", Cargas.VarreduraComQuente(Paginas, 50, Acessos, 11)),
    };

    foreach (var capacidade in new[] { 50, 100 })
    {
        Console.WriteLine($"capacidade {capacidade}");

        Console.WriteLine($"  {"carga",-20} {"otimo",7} {"LRU",7} {"FIFO",7} {"2a ch.",7} {"LFU",7}");

        foreach (var (nome, cadeia) in cargas)
        {
            string Taxa(IPolitica politica) => $"{Simulacao.Rodar(politica, cadeia).Taxa * 100,6:F1}%";

            Console.WriteLine($"  {nome,-20} {Taxa(new Otimo(capacidade, cadeia)),7} {Taxa(new Lru(capacidade)),7} "
                + $"{Taxa(new Fifo(capacidade)),7} {Taxa(new SegundaChance(capacidade)),7} {Taxa(new Lfu(capacidade)),7}");
        }
    }
}
