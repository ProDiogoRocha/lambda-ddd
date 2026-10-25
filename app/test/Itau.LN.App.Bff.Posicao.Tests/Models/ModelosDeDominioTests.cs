using Itau.LN.App.Bff.Posicao.Domain.Models;

namespace Itau.LN.App.Bff.Posicao.Tests.Models;

// Os modelos do domínio são records imutáveis: igualdade por valor e cópia com `with`.
public class ModelosDeDominioTests
{
    [Fact]
    public void ModelosComOsMesmosValores_SaoIguais()
    {
        Assert.Equal(new Cedula("100", 1m, 0m, 0m, 2m), new Cedula("100", 1m, 0m, 0m, 2m));
        Assert.Equal(new Moeda("1R", 1m, 0m), new Moeda("1R", 1m, 0m));
        Assert.Equal(new SaldoCustodia("B", 1m, 2m, 3m, 4m), new SaldoCustodia("B", 1m, 2m, 3m, 4m));
        Assert.Equal(new Resumo(1m, 2m, 3m, 4m), new Resumo(1m, 2m, 3m, 4m));
        Assert.Equal(new JustificativaDivergencias("IDX", 1m, "a", 2m, "b"), new JustificativaDivergencias("IDX", 1m, "a", 2m, "b"));
        Assert.Equal(new TermoJuridico("IDX", "t"), new TermoJuridico("IDX", "t"));
    }

    [Fact]
    public void With_AlteraSoOCampoInformadoSemMexerNoOriginal()
    {
        var original = new Resumo(1m, 2m, 3m, 4m);

        var alterado = original with { TotalDivergencia = 9m };

        Assert.Equal(new Resumo(1m, 2m, 9m, 4m), alterado);
        Assert.Equal(new Resumo(1m, 2m, 3m, 4m), original);
    }

    // Record compara coleções por referência: mesmo conteúdo em outra lista deixa o card diferente.
    [Fact]
    public void CardPosicao_ComparaColecoesPorReferencia()
    {
        var card = new CardPosicao(
            "IDX", "P1", "Tipo", 1m, "NA", "2026-10-25", "10:00", "Fechada", 0m,
            [], [], [new Moeda("1R", 1m, 0m)], new Resumo(1m, 0m, 0m, 1m), "", "", "");

        Assert.Equal(card, card with { });
        Assert.NotEqual(card, card with { Moedas = [new Moeda("1R", 1m, 0m)] });
    }
}
