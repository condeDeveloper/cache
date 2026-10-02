# cache

Políticas de substituição de cache (LRU, FIFO, LFU e segunda chance) do zero em
C# e .NET 8, medidas contra o ótimo de Belady, a política que ninguém consegue
bater.

```
$ dotnet medidor.dll carga

capacidade 50
  carga                  otimo     LRU    FIFO  2a ch.     LFU
  zipf 1.0               66.3%   46.5%   41.0%   48.1%   56.2%
  laco de 120            40.9%    0.0%    0.0%    0.0%    0.0%
  varredura + quente     50.0%   22.1%   19.5%   24.2%   48.4%
```

## O juiz é um teorema, e o teorema tem juiz

A regra de Belady (1966) é uma linha: quando precisar abrir espaço, descarte o
item cujo próximo uso está mais longe. Nenhuma política faz menos faltas. Ela é
impossível de usar de verdade, porque precisa conhecer o futuro, e é por isso
mesmo que serve de régua: sem ela, dizer que o LRU "vai bem" é opinião.

Só que teorema aplicado a código pode ter sido aplicado errado. Então o próprio
ótimo é conferido contra uma busca exaustiva que não usa regra nenhuma: ela tenta
**todas** as escolhas de despejo e fica com a melhor. As duas precisam bater em
toda cadeia de até oito acessos sobre quatro páginas.

Depois disso, nenhuma política pode fazer menos faltas que o ótimo em cadeia
nenhuma, o LRU rápido (lista + dicionário) tem de decidir igual ao LRU óbvio
(lista que se percorre) em toda cadeia, e nenhum cache passa da capacidade.

São 15 testes.

## A anomalia de Belady, achada em vez de citada

A intuição diz que um cache maior nunca é pior. Belady, Nelson e Shedler
mostraram em 1969 que, no FIFO, é: há cadeias em que quatro quadros fazem mais
faltas que três.

Aqui o teste não cita o exemplo, ele procura a menor cadeia em que isso acontece.
O resultado, com até cinco páginas:

| política | menor cadeia | faltas |
|---|---|---|
| FIFO | `0 1 2 3 0 1 4 0 1 2 3 4` (12 acessos) | 3 quadros: 9, 4 quadros: 10 |
| segunda chance | `0 0 1 1 2 3 2` (7 acessos) | 2 quadros: 4, 3 quadros: 5 |
| LFU | `0 0 1 2 1 2 3 0 3 0` (10 acessos) | 2 quadros: 6, 3 quadros: 7 |
| LRU e ótimo | nenhuma | |

A linha do FIFO é a cadeia do artigo de 1969, `1 2 3 4 1 2 5 1 2 3 4 5`, só com as
páginas renomeadas. Não existe anomalia do FIFO menor que ela.

O LRU e o ótimo não têm anomalia por um motivo que o teste também confere: a
**propriedade de pilha**. Depois de cada acesso, o conteúdo do cache de tamanho
k está contido no de tamanho k+1. O FIFO quebra essa propriedade numa fração
grande dos passos, e a anomalia é consequência.

## Varrer só o que importa

Nenhuma política daqui olha o valor da página, só a ordem em que ela aparece.
Então `[7, 3, 7]` e `[0, 1, 0]` fazem as mesmas faltas, e varrer as duas é
desperdício. A varredura usa só as cadeias canônicas, que nomeiam as páginas pela
ordem da primeira aparição:

```
$ dotnet medidor.dll contar

paginas  acessos  todas          canonicas    razao
      5        9     2,441,406       22,980   106.2x
      5       10    12,207,031      109,452   111.5x
      5       11    61,035,156      531,457   114.8x
```

A economia só vale se a premissa for verdadeira, e ela é testada: duas mil cadeias
rodam de novo com as páginas trocadas por uma permutação, em todas as políticas,
e as faltas têm de bater.

## O que as medidas me corrigiram

- **Eu tinha escrito que a menor anomalia do FIFO tinha nove acessos.** Não tem
  nenhuma com menos de doze. Com a varredura completa o teste nem terminava, e foi
  por isso que o número errado sobreviveu: ele nunca tinha sido conferido.
- **Achei que a segunda chance herdaria a anomalia do FIFO no mesmo tamanho.**
  Ela aparece com sete acessos, bem antes. Com dois quadros, o bit de uso dá à
  página 0 uma segunda vida, e isso muda quem sai.
- **A primeira versão do ótimo procurava o próximo uso varrendo o futuro a cada
  despejo.** Com vinte mil acessos e cem quadros, eram mais de dez bilhões de
  comparações. Agora o próximo uso de cada posição sai de uma passada de trás
  para a frente, e cada despejo custa uma volta pelo cache.

## As cargas

Política de cache não tem nota absoluta, tem nota por carga:

- **zipf**: poucas páginas recebem quase tudo, como em cache web. O LFU chega
  mais perto do ótimo que o LRU.
- **laço**: um laço um pouco maior que o cache zera o LRU e o FIFO, porque cada
  página sai logo antes de voltar. O ótimo acerta 40,9% com 50 quadros num laço
  de 120.
- **varredura com conjunto quente**: metade dos acessos é uma varredura que nunca
  se repete. Ela empurra o conjunto quente para fora do LRU, e o contador do LFU
  segura. Como metade dos acessos nunca volta, 50% é o teto de qualquer política.

## Como rodar

```
dotnet test testes/Cache.Testes/Cache.Testes.csproj -c Release
dotnet run --project ferramentas/Medidor/Medidor.csproj -c Release -- carga
dotnet run --project ferramentas/Medidor/Medidor.csproj -c Release -- anomalia
```

## Licença

MIT.
