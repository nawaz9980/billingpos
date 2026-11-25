using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace QuickPos.Services;

/// <summary>
/// Service to send raw ESC/POS commands to thermal receipt printers (58mm / 80mm).
/// Supports Windows Spooler (USB/Serial/Generic) and Raw TCP Socket (Network / Ethernet / Wi-Fi).
/// </summary>
public static class ThermalPrinterService
{
    // ESC/POS Command Constants
    public static readonly byte[] EscInit = [0x1B, 0x40];                   // Initialize printer
    public static readonly byte[] EscAlignLeft = [0x1B, 0x61, 0x00];         // Align left
    public static readonly byte[] EscAlignCenter = [0x1B, 0x61, 0x01];       // Align center
    public static readonly byte[] EscAlignRight = [0x1B, 0x61, 0x02];        // Align right
    public static readonly byte[] EscBoldOn = [0x1B, 0x45, 0x01];            // Bold on
    public static readonly byte[] EscBoldOff = [0x1B, 0x45, 0x00];           // Bold off
    public static readonly byte[] EscDoubleSize = [0x1D, 0x21, 0x11];        // Double width & height
    public static readonly byte[] EscNormalSize = [0x1D, 0x21, 0x00];        // Normal text size
    public static readonly byte[] EscCutPaper = [0x1D, 0x56, 0x41, 0x10];    // Cut paper
    public static readonly byte[] EscOpenDrawer = [0x1B, 0x70, 0x00, 0x19, 0xFA]; // Open cash drawer

    [DllImport("winspool.drv", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GetDefaultPrinter(StringBuilder pszBuffer, ref int pcchBuffer);

    /// <summary>
    /// Gets the name of the default Windows printer.
    /// </summary>
    public static string GetDefaultPrinterName()
    {
        try
        {
            int pcchBuffer = 260;
            var pszBuffer = new StringBuilder(pcchBuffer);
            if (GetDefaultPrinter(pszBuffer, ref pcchBuffer))
            {
                return pszBuffer.ToString();
            }
        }
        catch { }
        return string.Empty;
    }

    /// <summary>
    /// Converts a numeric amount into Indian Rupee words (e.g. Rupee Seven Thousand One Hundred Ten Only).
    /// </summary>
    public static string AmountToWords(decimal amount)
    {
        long whole = (long)Math.Floor(amount);
        int paise = (int)Math.Round((amount - whole) * 100);

        string words = NumberToWords(whole);
        string result = $"Rupee {words}";
        if (paise > 0)
        {
            result += $" and {NumberToWords(paise)} Paise";
        }
        result += " Only";
        return result;
    }

    private static string NumberToWords(long number)
    {
        if (number == 0) return "Zero";
        if (number < 0) return "Minus " + NumberToWords(Math.Abs(number));

        string words = "";

        if (number / 10000000 > 0)
        {
            words += NumberToWords(number / 10000000) + " Crore ";
            number %= 10000000;
        }

        if (number / 100000 > 0)
        {
            words += NumberToWords(number / 100000) + " Lakh ";
            number %= 100000;
        }

        if (number / 1000 > 0)
        {
            words += NumberToWords(number / 1000) + " Thousand ";
            number %= 1000;
        }

        if (number / 100 > 0)
        {
            words += NumberToWords(number / 100) + " Hundred ";
            number %= 100;
        }

        if (number > 0)
        {
            var unitsMap = new[] { "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
            var tensMap = new[] { "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

            if (number < 20)
                words += unitsMap[number];
            else
            {
                words += tensMap[number / 10];
                if ((number % 10) > 0)
                    words += " " + unitsMap[number % 10];
            }
        }

        return words.Trim();
    }

    /// <summary>
    /// Builds authentic Retail POS Tax Invoice receipt bytes & text matching the exact reference bill layout.
    /// </summary>
    public static (byte[] EscPosBytes, string ReceiptText) BuildReceipt(
        string storeName,
        string billNo,
        IEnumerable<(string Code, string Item, int Qty, decimal Rate, decimal Amount)> items,
        decimal subTotal,
        decimal tax,
        decimal grandTotal,
        string tendered,
        string change,
        string tenderMode = "Cash",
        string rrn = "",
        string customerName = "Rajesh Kumar")
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);
        var sbText = new StringBuilder();

        const string sepSingle = "------------------------------------------";
        const string sepDouble = "==========================================";
        const string sepDashed = "=== = = = = = = = = = = = = = = = = = = ==";

        var itemList = items.ToList();
        int totalQty = itemList.Sum(i => i.Qty);
        decimal itemDiscount = 0.00m;
        decimal grossAmount = grandTotal;
        decimal billDiscount = 0.00m;

        decimal sgstAmt = Math.Round(tax / 2m, 2);
        decimal cgstAmt = tax - sgstAmt;
        decimal taxable = grandTotal - tax;

        // Initialize printer
        writer.Write(EscInit);

        // ══════════════ 1. HEADER (Store Name & Address) ══════════════
        writer.Write(EscAlignCenter);
        writer.Write(EscBoldOn);
        writer.Write(Encoding.ASCII.GetBytes($"{storeName}\n"));
        writer.Write(EscBoldOff);
        writer.Write(Encoding.ASCII.GetBytes($"{sepSingle}\n"));
        writer.Write(Encoding.ASCII.GetBytes("GL One Mall, Ground Floor, Main Road,\nPuttur\n"));
        writer.Write(Encoding.ASCII.GetBytes("Ph. M. 8296090426\n"));
        writer.Write(Encoding.ASCII.GetBytes("E Mail : whitetag.puttur@gmail.com\n"));
        writer.Write(Encoding.ASCII.GetBytes("GSTIN = 29AAPCW8472M1ZK\n"));
        writer.Write(Encoding.ASCII.GetBytes($"{sepDouble}\n"));

        sbText.AppendLine($"                {storeName}");
        sbText.AppendLine(sepSingle);
        sbText.AppendLine("  GL One Mall, Ground Floor, Main Road,");
        sbText.AppendLine("                 Puttur");
        sbText.AppendLine("           Ph. M. 8296090426");
        sbText.AppendLine("   E Mail : whitetag.puttur@gmail.com");
        sbText.AppendLine("        GSTIN = 29AAPCW8472M1ZK");
        sbText.AppendLine(sepDouble);

        // ══════════════ 2. BILL META (Bill No, Date, Time) ══════════════
        writer.Write(EscAlignLeft);
        string safeBillClean = billNo.Replace("#", "").Trim();
        string billDate = DateTime.Now.ToString("dd-MMM-yyyy");
        string billTime = DateTime.Now.ToString("HH:mm:ss");

        string metaLine1 = $"Bill No.: {safeBillClean,-15} Date : {billDate}\n";
        string metaLine2 = $"{"",-25} Time : {billTime}\n";
        writer.Write(Encoding.ASCII.GetBytes(metaLine1));
        writer.Write(Encoding.ASCII.GetBytes(metaLine2));
        writer.Write(Encoding.ASCII.GetBytes($"{sepSingle}\n"));

        sbText.Append(metaLine1);
        sbText.Append(metaLine2);
        sbText.AppendLine(sepSingle);

        // ══════════════ 3. CUSTOMER DETAILS ══════════════
        string custLine1 = $"To, {customerName}\n";
        string custLine2 = "Puttur Main Road, Ph : 9845123456\n";
        string custLine3 = "GSTIN : Unregistered\n";
        writer.Write(Encoding.ASCII.GetBytes(custLine1));
        writer.Write(Encoding.ASCII.GetBytes(custLine2));
        writer.Write(Encoding.ASCII.GetBytes(custLine3));
        writer.Write(Encoding.ASCII.GetBytes($"{sepSingle}\n"));

        sbText.Append(custLine1);
        sbText.Append(custLine2);
        sbText.Append(custLine3);
        sbText.AppendLine(sepSingle);

        // ══════════════ 4. ITEM TABLE ══════════════
        string colHeader = "Sr. Description          Qty    Rate    Amount\n";
        writer.Write(Encoding.ASCII.GetBytes(colHeader));
        writer.Write(Encoding.ASCII.GetBytes($"{sepSingle}\n"));

        sbText.Append(colHeader);
        sbText.AppendLine(sepSingle);

        int srNo = 1;
        foreach (var it in itemList)
        {
            string desc = it.Item.Length > 19 ? it.Item[..16] + "..." : it.Item;
            string row = $"{srNo,2}  {desc,-19} {it.Qty,3}  {it.Rate,6:F2}  {it.Amount,8:F2}\n";
            writer.Write(Encoding.ASCII.GetBytes(row));
            sbText.Append(row);
            srNo++;
        }

        writer.Write(Encoding.ASCII.GetBytes($"{sepSingle}\n"));
        string qtyLine = $"Total Qty              {totalQty,4}\n";
        string discLine = $"Item Discount Inclusive       {itemDiscount,6:F2}\n";
        writer.Write(Encoding.ASCII.GetBytes(qtyLine));
        writer.Write(Encoding.ASCII.GetBytes(discLine));
        writer.Write(Encoding.ASCII.GetBytes($"{sepDouble}\n"));

        sbText.AppendLine(sepSingle);
        sbText.Append(qtyLine);
        sbText.Append(discLine);
        sbText.AppendLine(sepDouble);

        // ══════════════ 5. AMOUNTS & NET AMOUNT ══════════════
        string grossLine = $"Gross Amount After Discount     {grossAmount,10:F2}\n";
        string billDiscLine = $"Bill Discount                   {billDiscount,10:F2}\n";
        writer.Write(Encoding.ASCII.GetBytes(grossLine));
        writer.Write(Encoding.ASCII.GetBytes(billDiscLine));

        sbText.Append(grossLine);
        sbText.Append(billDiscLine);

        // Net Amount Highlight
        writer.Write(EscBoldOn);
        string netLine = $"Net Amount                   Rs.{grandTotal,10:F2}\n";
        writer.Write(Encoding.ASCII.GetBytes(netLine));
        writer.Write(EscBoldOff);
        writer.Write(Encoding.ASCII.GetBytes($"{sepDashed}\n"));

        sbText.Append(netLine);
        sbText.AppendLine(sepDashed);

        // Amount in Words
        string words = AmountToWords(grandTotal);
        string wordsLine = $"{words}\n";
        writer.Write(Encoding.ASCII.GetBytes(wordsLine));
        sbText.Append(wordsLine);

        // ══════════════ 6. TENDER DETAILS ══════════════
        writer.Write(EscAlignCenter);
        writer.Write(Encoding.ASCII.GetBytes("Tender Details\n"));
        writer.Write(EscAlignLeft);

        decimal adv = grandTotal;
        decimal bal = 0.00m;
        string advLine = $"Advance Amount                  {adv,10:F2}\n";
        string balLine = $"Balance Amount                  {bal,10:F2}\n";
        writer.Write(Encoding.ASCII.GetBytes(advLine));
        writer.Write(Encoding.ASCII.GetBytes(balLine));

        sbText.AppendLine("              Tender Details");
        sbText.Append(advLine);
        sbText.Append(balLine);

        // Tender Modes breakdown
        string cashTender = tenderMode == "Cash" ? $"{grandTotal:F2}" : "0.00";
        string upiTender = tenderMode == "UPI" || tenderMode == "UPI/Paytm" ? $"{grandTotal:F2}" : "0.00";
        string cardTender = tenderMode == "Debit Card" || tenderMode == "Credit Card" ? $"{grandTotal:F2}" : "0.00";
        string rrnPart = string.IsNullOrWhiteSpace(rrn) ? "" : $" RRN:{rrn}";

        string modeLine = $"Cash : {cashTender}, UPI : {upiTender}, Card : {cardTender}{rrnPart}\n";
        writer.Write(Encoding.ASCII.GetBytes(modeLine));
        writer.Write(Encoding.ASCII.GetBytes($"{sepDouble}\n"));

        sbText.Append(modeLine);
        sbText.AppendLine(sepDouble);

        // ══════════════ 7. GST TAX BREAKDOWN & GREETINGS ══════════════
        writer.Write(EscAlignCenter);
        writer.Write(Encoding.ASCII.GetBytes("Have a Nice Day\nThanks for your Kind Visit\n"));
        writer.Write(Encoding.ASCII.GetBytes("DETAILS OF GST TAX\n"));
        writer.Write(EscAlignLeft);

        string taxHeader = "Taxable       SGST%    Amt.     CGST%    Amt.\n";
        string taxValues = $"{taxable,8:F2}       2.5%  {sgstAmt,6:F2}     2.5%  {cgstAmt,6:F2}\n";
        writer.Write(Encoding.ASCII.GetBytes(taxHeader));
        writer.Write(Encoding.ASCII.GetBytes(taxValues));
        writer.Write(Encoding.ASCII.GetBytes($"{sepDouble}\n\n\n\n"));
        writer.Write(EscCutPaper);

        sbText.AppendLine("             Have a Nice Day");
        sbText.AppendLine("       Thanks for your Kind Visit");
        sbText.AppendLine("           DETAILS OF GST TAX");
        sbText.Append(taxHeader);
        sbText.Append(taxValues);
        sbText.AppendLine(sepDouble);

        return (ms.ToArray(), sbText.ToString());
    }

    /// <summary>
    /// Sends raw bytes to a Windows printer queue (USB / Thermal / Generic).
    /// </summary>
    public static bool PrintRawToPrinter(string printerName, byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            return false;

        return RawPrinterHelper.SendBytesToPrinter(printerName, bytes);
    }

    /// <summary>
    /// Prints receipt data to a network thermal printer over port 9100.
    /// </summary>
    public static void PrintNetwork(string ipAddress, int port, byte[] data)
    {
        using var client = new TcpClient(ipAddress, port);
        using var stream = client.GetStream();
        stream.Write(data, 0, data.Length);
        stream.Flush();
    }
}

/// <summary>
/// Win32 Spooler API Helper to send raw bytes to physical Windows printers.
/// </summary>
internal static class RawPrinterHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)] public string? pDocName;
        [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)] public string? pDataType;
    }

    [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

    [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

    [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
    public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    public static bool SendBytesToPrinter(string szPrinterName, byte[] bytes)
    {
        IntPtr pUnmanagedBytes = Marshal.AllocCoTaskMem(bytes.Length);
        Marshal.Copy(bytes, 0, pUnmanagedBytes, bytes.Length);

        bool success = false;
        try
        {
            if (OpenPrinter(szPrinterName, out IntPtr hPrinter, IntPtr.Zero))
            {
                var di = new DOCINFOA { pDocName = "QuickPOS Receipt", pDataType = "RAW" };
                if (StartDocPrinter(hPrinter, 1, di))
                {
                    if (StartPagePrinter(hPrinter))
                    {
                        success = WritePrinter(hPrinter, pUnmanagedBytes, bytes.Length, out _);
                        EndPagePrinter(hPrinter);
                    }
                    EndDocPrinter(hPrinter);
                }
                ClosePrinter(hPrinter);
            }
        }
        catch { }
        finally
        {
            Marshal.FreeCoTaskMem(pUnmanagedBytes);
        }

        return success;
    }
}
