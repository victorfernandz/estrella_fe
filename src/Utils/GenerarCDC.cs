using System;

public class GenerarCDC
{
    public static string GenerarCodigoCDC(string iTiDE, string dRucEm, string dDVEmi, string dEst, string dPunExp, string dNumDoc, string dTipCont, string dFeEmiDE, string iTipEmi, string dCodSeg)
    {
        string cdcBase = $"{iTiDE.Trim()}{dRucEm.Trim()}{dDVEmi.Trim()}{dEst.Trim()}{dPunExp.Trim()}{dNumDoc.Trim()}{dTipCont.Trim()}{dFeEmiDE.Trim()}{iTipEmi.Trim()}{dCodSeg.Trim()}";
        int dv = CalcularDV(cdcBase);
        return cdcBase + dv;
    }

    public static int CalcularDV(string numero, int baseMax = 11)
    {
        // 1. Convertir caracteres alfanuméricos a código ASCII si no son dígitos
        string numeroConvertido = "";
        foreach (char c in numero.ToUpper())
        {
            if (char.IsDigit(c))
            {
                numeroConvertido += c;
            }
            else
            {
                numeroConvertido += ((int)c).ToString(); 
            }
        }

        // 2. Aplicar pesos de derecha a izquierda, del 2 a baseMax cíclicamente
        int total = 0;
        int k = 2;

        for (int i = numeroConvertido.Length - 1; i >= 0; i--)
        {
            if (k > baseMax) k = 2;
            int digito = int.Parse(numeroConvertido[i].ToString());
            total += digito * k;
            k++;
        }

        // 3. Calcular resto y dígito verificador según la regla
        int resto = total % 11;
        int dv = resto > 1 ? 11 - resto : 0;

        return dv;
    }

}
