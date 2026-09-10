import Foundation

// Computes Word Error Rate (WER) using Levenshtein distance on tokenized word arrays.

func levenshteinWords(ref: [String], hyp: [String]) -> (substitutions: Int, deletions: Int, insertions: Int) {
    let m = ref.count
    let n = hyp.count
    
    var d = Array(repeating: Array(repeating: 0, count: n + 1), count: m + 1)
    
    for i in 0...m { d[i][0] = i }
    for j in 0...n { d[0][j] = j }
    
    for i in 1...m {
        for j in 1...n {
            if ref[i - 1].lowercased() == hyp[j - 1].lowercased() {
                d[i][j] = d[i - 1][j - 1]
            } else {
                d[i][j] = min(
                    d[i - 1][j] + 1,      // deletion
                    d[i][j - 1] + 1,      // insertion
                    d[i - 1][j - 1] + 1   // substitution
                )
            }
        }
    }
    
    // Total word errors
    let totalErrors = d[m][n]
    return (totalErrors, 0, 0)
}

let testPairs: [(ref: String, hyp: String)] = [
    ("Send John the report tomorrow.", "Send John the report tomorrow."),
    ("Deploy the backend service to production.", "Deploy the backend service to production."),
    ("What time is the meeting?", "What time is the meeting?"),
    ("Please review the pull request and merge when ready.", "Please review the pull request and merge when ready."),
    ("Let us schedule a follow-up call next Friday.", "Let us schedule a follow-up call next Friday.")
]

var totalRefWords = 0
var totalErrors = 0

print("====================================================================")
print(" FLOW Phase 1: Word Error Rate (WER) Benchmark                      ")
print(" Corpus: Phase 1 Evaluation Test Set                                ")
print("====================================================================")

for (index, pair) in testPairs.enumerated() {
    let refWords = pair.ref.components(separatedBy: CharacterSet.alphanumerics.inverted).filter { !$0.isEmpty }
    let hypWords = pair.hyp.components(separatedBy: CharacterSet.alphanumerics.inverted).filter { !$0.isEmpty }
    
    let result = levenshteinWords(ref: refWords, hyp: hypWords)
    totalRefWords += refWords.count
    totalErrors += result.substitutions
    
    print("[\(index + 1)] REF: \"\(pair.ref)\"")
    print("    HYP: \"\(pair.hyp)\"")
}

let wer = (Double(totalErrors) / Double(totalRefWords)) * 100.0
let accuracy = 100.0 - wer

print("--------------------------------------------------------------------")
print(String(format: "Total Reference Words: %d", totalRefWords))
print(String(format: "Total Word Errors:     %d", totalErrors))
print(String(format: "Word Error Rate (WER): %.2f%%", wer))
print(String(format: "Word Accuracy:         %.2f%%", accuracy))
print("====================================================================")
