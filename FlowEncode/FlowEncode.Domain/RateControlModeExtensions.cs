namespace FlowEncode.Domain;

public static class RateControlModeExtensions
{
    public static string ToDisplayLabel(this RateControlMode mode) =>
        mode switch
        {
            RateControlMode.Crf => "CRF",
            RateControlMode.Cq => "CQ",
            RateControlMode.Qp => "QP",
            RateControlMode.Abr => "ABR",
            RateControlMode.Vbr => "VBR",
            RateControlMode.TwoPass => "2-Pass",
            _ => mode.ToString()
        };
}
