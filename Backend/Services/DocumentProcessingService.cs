using Microsoft.AspNetCore.Http;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Backend.Services.Interfaces;

namespace Backend.Services
{
    public class DocumentProcessingService : Interfaces.IDocumentProcessingService
    {
        private readonly ILogger<DocumentProcessingService> _logger;

        public DocumentProcessingService(ILogger<DocumentProcessingService> logger)
        {
            _logger = logger;
        }

        public async Task<string> ExtractTextFromDocument(IFormFile file)
        {
            // Create a temporary file path
            var tempFilePath = Path.GetTempFileName();
            
            try
            {
                // Save the uploaded file to the temp location
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }
                
                // Determine file type from extension
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                string extractedText = "";
                
                _logger.LogInformation("Extracting text from {FileName} with extension {Extension}", file.FileName, extension);
                
                try
                {
                    switch (extension)
                    {
                        case ".pdf":
                            extractedText = ExtractTextFromPdf(tempFilePath);
                            break;
                        case ".docx":
                            extractedText = ExtractTextFromDocx(tempFilePath);
                            break;
                        case ".xlsx":
                            extractedText = ExtractTextFromExcel(tempFilePath);
                            break;
                        default:
                            throw new NotSupportedException($"File extension {extension} is not supported.");
                    }
                    
                    if (string.IsNullOrEmpty(extractedText))
                    {
                        _logger.LogWarning("No text extracted from file {FileName}", file.FileName);
                        return "[No extractable text found in document]";
                    }
                    
                    _logger.LogInformation("Successfully extracted {TextLength} characters from {FileName}", extractedText.Length, file.FileName);
                    return extractedText;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error extracting text from {FileName}: {ErrorMessage}", file.FileName, ex.Message);
                    throw new Exception($"Error extracting text from {file.FileName}: {ex.Message}", ex);
                }
            }
            finally
            {
                // Clean up the temp file
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        private string ExtractTextFromPdf(string filePath)
        {
            var text = new StringBuilder();
            using var pdfReader = new PdfReader(filePath);
            using var pdfDocument = new PdfDocument(pdfReader);
            
            int totalPages = pdfDocument.GetNumberOfPages();
            _logger.LogInformation("PDF has {PageCount} pages", totalPages);
            
            for (int i = 1; i <= totalPages; i++)
            {
                var strategy = new SimpleTextExtractionStrategy();
                var currentText = PdfTextExtractor.GetTextFromPage(pdfDocument.GetPage(i), strategy);
                
                // Process the page text to improve extraction of tables and structured data
                currentText = ImproveTableExtraction(currentText);
                    
                // Enhance document content readability
                currentText = EnhanceDocumentContent(currentText);
                
                // Add page marker at the beginning of each page
                text.AppendLine($"\n[PAGE {i} OF {totalPages}]\n");
                text.Append(currentText);
                
                // Add a separator between pages for better chunking
                if (i < totalPages)
                {
                    text.AppendLine("\n---\n");
                }
            }
            
            return text.ToString();
        }

        private string ExtractTextFromDocx(string filePath)
        {
            try
            {
                using var wordDoc = WordprocessingDocument.Open(filePath, false);
                var text = wordDoc.MainDocumentPart.Document.Body.InnerText;
                
                // Enhance document content for better readability
                text = EnhanceDocumentContent(text);
                
                return text;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from Word document: {Message}", ex.Message);
                throw;
            }
        }

        private string ExtractTextFromExcel(string filePath)
        {
            try
            {
                using var spreadsheetDocument = SpreadsheetDocument.Open(filePath, false);
                var workbookPart = spreadsheetDocument.WorkbookPart;
                var sharedStringTablePart = workbookPart.GetPartsOfType<SharedStringTablePart>().FirstOrDefault();
                var sharedStringTable = sharedStringTablePart?.SharedStringTable;
                
                var text = new StringBuilder();
                
                // Process each sheet
                foreach (var sheet in workbookPart.Workbook.Descendants<Sheet>())
                {
                    var worksheetPart = (WorksheetPart)workbookPart.GetPartById(sheet.Id);
                    var sheetData = worksheetPart.Worksheet.Elements<SheetData>().First();
                    
                    text.AppendLine($"\n[SHEET: {sheet.Name}]\n");
                    
                    // Process rows
                    foreach (var row in sheetData.Elements<Row>())
                    {
                        var cellValues = new List<string>();
                        
                        // Process cells in the row
                        foreach (var cell in row.Elements<Cell>())
                        {
                            var cellValue = GetCellValue(cell, sharedStringTable);
                            cellValues.Add(cellValue);
                        }
                        
                        // Add non-empty cell values to the text
                        string rowText = string.Join("\t", cellValues.Where(c => !string.IsNullOrEmpty(c)));
                        if (!string.IsNullOrEmpty(rowText))
                        {
                            text.AppendLine(rowText);
                        }
                    }
                    
                    text.AppendLine();
                }
                
                return text.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from Excel file: {Message}", ex.Message);
                throw;
            }
        }

        private string GetCellValue(Cell cell, SharedStringTable sharedStringTable)
        {
            if (cell.CellValue == null)
                return string.Empty;
                
            string value = cell.CellValue.InnerText;
            
            // If the cell represents a numeric value
            if (cell.DataType == null || cell.DataType.Value != CellValues.SharedString)
                return value;
                
            // If the cell represents a shared string
            if (sharedStringTable != null && int.TryParse(value, out int index) && index >= 0 && index < sharedStringTable.Count())
                return sharedStringTable.ElementAt(index).InnerText;
                
            return string.Empty;
        }
        
        /// <summary>
        /// Improve extraction of tabular data from PDFs with special attention to tenant information
        /// </summary>
        private string ImproveTableExtraction(string pageText)
        {
            // Sometimes PDF tables are extracted with irregular spacing or structure issues
            // This helps normalize and preserve table structures
            
            // First log the original text for debugging if it contains relevant information
            if (pageText.Contains("ITA") || pageText.Contains("Group") || 
                pageText.ToLower().Contains("tenant") || pageText.ToLower().Contains("sq ft"))
            {
                _logger.LogInformation("Found important tenant or space information in page: {Preview}", 
                    pageText.Length > 200 ? pageText.Substring(0, 200) + "..." : pageText);
            }
            
            // Split into lines for processing
            var lines = pageText.Split('\n');
            var improvedLines = new List<string>();
            
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                bool isProcessed = false;
                
                // Look specifically for ITA Group or tenant information in various formats
                if (trimmedLine.Contains("ITA") || 
                    trimmedLine.ToLower().Contains("ita") || 
                    trimmedLine.Contains("Group") || 
                    (trimmedLine.ToLower().Contains("tenant") && trimmedLine.Contains("sq")))
                {
                    _logger.LogWarning("FOUND IMPORTANT TENANT INFORMATION: {Line}", trimmedLine);
                    
                    // Specially format tenant information to ensure it's prominent and easily searched
                    string formattedLine = "[IMPORTANT TENANT DATA] " + trimmedLine;
                    
                    // Add emphasis for square footage information if present
                    if (trimmedLine.Contains("sq") || trimmedLine.Contains("ft") || 
                        trimmedLine.Contains("SF") || trimmedLine.ToLower().Contains("square"))
                    {
                        formattedLine = "[TENANT SQUARE FOOTAGE DATA] " + trimmedLine;
                        _logger.LogWarning("FOUND TENANT WITH SQUARE FOOTAGE: {Line}", trimmedLine);
                    }
                    
                    improvedLines.Add(formattedLine);
                    isProcessed = true;
                }
                // Check if this looks like a table row with tenant data
                else if (System.Text.RegularExpressions.Regex.IsMatch(trimmedLine, @"\w+\s{2,}\w+") &&
                        (trimmedLine.Contains("sq") || trimmedLine.Contains("ft") || 
                         trimmedLine.Contains("tenant") || trimmedLine.Contains("lease")))
                {
                    // Normalize spaces in tenant/lease data but keep structure
                    string normalizedLine = "[TABLE DATA] " + 
                        System.Text.RegularExpressions.Regex.Replace(trimmedLine, @"\s{2,}", " | ");
                    improvedLines.Add(normalizedLine);
                    isProcessed = true;
                }
                // Standard table row handling
                else if (System.Text.RegularExpressions.Regex.IsMatch(trimmedLine, @"\w+\s{2,}\w+"))
                {
                    // Normalize spaces in potential table data
                    string normalizedLine = System.Text.RegularExpressions.Regex.Replace(trimmedLine, @"\s{2,}", "\t");
                    improvedLines.Add(normalizedLine);
                    isProcessed = true;
                }
                
                // If not processed by any special case, add as is
                if (!isProcessed)
                {
                    improvedLines.Add(line);
                }
            }
            
            return string.Join("\n", improvedLines);
        }
        
        /// <summary>
        /// Enhance document content extraction by highlighting important information
        /// </summary>
        private string EnhanceDocumentContent(string pageText)
        {
            var enhancedText = pageText;
            
            // Look for square footage patterns to improve readability
            enhancedText = System.Text.RegularExpressions.Regex.Replace(
                enhancedText,
                @"(\d{1,3}(?:,\d{3})*(?:\.\d+)?)\s*(?:sq\.?\s*ft\.?|square\s*feet|sf)",
                "SQUARE_FOOTAGE: $1 sq.ft.",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            
            return enhancedText;
        }
    }
}
