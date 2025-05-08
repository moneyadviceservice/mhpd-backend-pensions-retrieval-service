using MhpdCommon.Models.MHPDModels;

namespace PensionsRetrievalFunction.Models;

public class PeiDataResponse
{
    private readonly Dictionary<string, PeiDataModel> _peiData = [];

    public PeiDataResponse(string? rpt, IEnumerable<PeiDataModel> peis)
    {
        AddPeiRange(peis);
        SetRpt(rpt);
    }

    public string? Rpt { get; private set; }

    public void SetRpt(string? rpt)
    {
        if (!string.IsNullOrEmpty(rpt))
        {
            Rpt = rpt;
        }
    }

    public void AddPeiRange(IEnumerable<PeiDataModel> peis)
    {
        if (peis == null) return;
        peis.ToList().ForEach(pei => _peiData.TryAdd(pei.Pei!, pei));
    }

    public bool TryAdd(PeiDataModel pei)
    {
        if(pei == null || pei.Pei == null) return false;
        return _peiData.TryAdd(pei.Pei, pei);
    }

    public IReadOnlyCollection<PeiDataModel> PeiData => _peiData.Values;
}
