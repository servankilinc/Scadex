public class ProxyService
{
    
    public string AlarmStateChange(int userid, string uDevId, string uCommand, int uPanelPass)
    {
        IProsecService prosec = new IProsecService();
        var result = prosec.spcArmDisArm(userid, uDevId, uCommand, uPanelPass);
        return result;
    }

    public string GetListOutput(int userid, string uDevId)
    {
        IProsecService prosec = new IProsecService();
        var result = prosec.spcGetPanels(userid, uDevId);
        return result;
    }

    public string GetListZoneStatus(int userid, string uDevId)
    {
        IProsecService prosec = new IProsecService();
        var result = prosec.spcGetPanelZonesByPassStatus(userid, uDevId);
        return result;
    }

    public string GetPanelInformation(int userid, string uDevId)
    {
        IProsecService prosec = new IProsecService();
        var result = prosec.spcGetPanels(userid, uDevId);
        return result;
    }

    public string SetOputputState(int userid, string uDevId, int outNo, int outPos)
    {
        IProsecService prosec = new IProsecService();
        var result = prosec.spcSetPanelOutputs(userid, uDevId, outNo, outPos);
        return result;
    }

    public string SetZoneState(int userid, string uDevId, int zOneNo, int zOnePos)
    {
        IProsecService prosec = new IProsecService();
        var result = prosec.spcSetPanelZonesByPassStatus(userid, uDevId, zOneNo, zOnePos);
        return result;
    }
}