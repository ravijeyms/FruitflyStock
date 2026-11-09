const PAGE_WIDTH = 612;
const PAGE_HEIGHT = 792;
const COLUMNS = 5;
const ROWS = 10;
const CELLS_PER_PAGE = COLUMNS * ROWS;
const MARGIN = 18;

document.addEventListener("click", event => {
    const button = event.target.closest("#printLabels");
    if (!button || button.disabled) {
        return;
    }

    const stocks = JSON.parse(button.dataset.stocks);
    downloadLabelPdf(stocks);
});

export function downloadLabelPdf(stocks) {
    if (!Array.isArray(stocks) || stocks.length === 0) {
        return;
    }

    const pageCount = Math.ceil(stocks.length / CELLS_PER_PAGE);
    const totalCells = pageCount * CELLS_PER_PAGE;
    const cellWidth = (PAGE_WIDTH - MARGIN * 2) / COLUMNS;
    const cellHeight = (PAGE_HEIGHT - MARGIN * 2) / ROWS;
    const objects = [
        "<< /Type /Catalog /Pages 2 0 R >>",
        "",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>"
    ];
    const pageReferences = [];

    for (let pageIndex = 0; pageIndex < pageCount; pageIndex++) {
        const pageObjectId = objects.length + 1;
        const contentObjectId = pageObjectId + 1;
        pageReferences.push(`${pageObjectId} 0 R`);

        const commands = [];
        for (let cellIndex = 0; cellIndex < CELLS_PER_PAGE; cellIndex++) {
            const globalCellIndex = pageIndex * CELLS_PER_PAGE + cellIndex;
            const stockIndex = globalCellIndex % stocks.length;
            const stock = stocks[stockIndex];
            const column = cellIndex % COLUMNS;
            const row = Math.floor(cellIndex / COLUMNS);
            const x = MARGIN + column * cellWidth;
            const y = PAGE_HEIGHT - MARGIN - (row + 1) * cellHeight;

            commands.push(
                "0.78 G 0.5 w",
                `${formatNumber(x)} ${formatNumber(y)} ${formatNumber(cellWidth)} ${formatNumber(cellHeight)} re S`);

            const labelLines = wrapText(stock.label, 8, cellWidth - 12);
            const genotypeLines = wrapText(stock.genotype, 6, cellWidth - 12);
            const textLines = [
                ...labelLines.map(text => ({ text, font: "F2", size: 8, lineHeight: 10 })),
                ...genotypeLines.map(text => ({ text, font: "F1", size: 6, lineHeight: 7 }))
            ];
            if (textLines.length === 0) {
                continue;
            }

            const textHeight = textLines.reduce((height, line) => height + line.lineHeight, 0);
            let baseline = y + (cellHeight + textHeight) / 2 - textLines[0].lineHeight;

            for (const line of textLines) {
                const safeText = toPdfText(line.text);
                const estimatedWidth = safeText.length * line.size * 0.5;
                const textX = x + Math.max(4, (cellWidth - estimatedWidth) / 2);
                commands.push(
                    "BT",
                    `/${line.font} ${line.size} Tf`,
                    `1 0 0 1 ${formatNumber(textX)} ${formatNumber(baseline)} Tm`,
                    `(${escapePdfText(safeText)}) Tj`,
                    "ET");
                baseline -= line.lineHeight;
            }
        }

        const content = commands.join("\n");
        objects.push(
            `<< /Type /Page /Parent 2 0 R /MediaBox [0 0 ${PAGE_WIDTH} ${PAGE_HEIGHT}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents ${contentObjectId} 0 R >>`,
            `<< /Length ${content.length} >>\nstream\n${content}\nendstream`);
    }

    objects[1] = `<< /Type /Pages /Kids [${pageReferences.join(" ")}] /Count ${pageCount} >>`;
    const pdf = serializePdf(objects);
    const blob = new Blob([pdf], { type: "application/pdf" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = "stock-labels.pdf";
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}

function wrapText(value, fontSize, maxWidth) {
    const text = toPdfText(value);
    if (text.length === 0) {
        return [];
    }

    const maxCharacters = Math.max(1, Math.floor(maxWidth / (fontSize * 0.52)));
    const words = text.split(/\s+/);
    const lines = [];
    let line = "";

    for (const word of words) {
        if (word.length > maxCharacters) {
            if (line) {
                lines.push(line);
                line = "";
            }
            for (let index = 0; index < word.length; index += maxCharacters) {
                lines.push(word.slice(index, index + maxCharacters));
            }
            continue;
        }

        const candidate = line ? `${line} ${word}` : word;
        if (candidate.length > maxCharacters && line) {
            lines.push(line);
            line = word;
        } else {
            line = candidate;
        }
    }

    if (line) {
        lines.push(line);
    }
    return lines;
}

function toPdfText(value) {
    return String(value ?? "")
        .replace(/[^\x20-\x7E]/g, "?");
}

function escapePdfText(value) {
    return value.replace(/[\\()]/g, character => `\\${character}`);
}

function formatNumber(value) {
    return Number(value.toFixed(2)).toString();
}

function serializePdf(objects) {
    let pdf = "%PDF-1.4\n";
    const offsets = [0];

    for (let index = 0; index < objects.length; index++) {
        offsets.push(pdf.length);
        pdf += `${index + 1} 0 obj\n${objects[index]}\nendobj\n`;
    }

    const crossReferenceOffset = pdf.length;
    pdf += `xref\n0 ${objects.length + 1}\n`;
    pdf += "0000000000 65535 f \n";
    for (let index = 1; index < offsets.length; index++) {
        pdf += `${String(offsets[index]).padStart(10, "0")} 00000 n \n`;
    }
    pdf += `trailer\n<< /Size ${objects.length + 1} /Root 1 0 R >>\n`;
    pdf += `startxref\n${crossReferenceOffset}\n%%EOF`;
    return pdf;
}
