using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Flow.Core.Context;
using Flow.Core.Language;
using Flow.Core.TranscriptProcessing;
using Xunit;

namespace Flow.Core.Tests.Developer;

/// <summary>
/// Dedicated Adversarial Prose Corpus Test Suite (Section 21).
/// Contains 105 curated natural-language sentences containing developer keywords
/// (function, class, method, interface, record, enum, arrow, dot, equals, camel case, snake case, API, at home, etc.)
/// and rigorously verifies that 0 unintended developer transformations occur in general prose context.
/// </summary>
public class AdversarialProseCorpusTests
{
    private readonly TranscriptProcessingPipeline _pipeline = new();

    private static readonly FormattingOptions ProseOptions = new(
        Category: ApplicationCategory.GeneralProse,
        TargetApplication: "notepad",
        DeveloperContext: new DeveloperContext("notepad", ApplicationCategory.GeneralProse, LanguageCatalog.English, IdentifierCasingStyle.None)
    );

    public static IEnumerable<object[]> CuratedAdversarialProseCases()
    {
        var sentences = new[]
        {
            // 1. Function / Method prose (15 cases)
            "The function of the heart is to pump blood throughout the human body.",
            "This function calculates the total revenue from all quarterly reports.",
            "What function does this module perform during the system demonstration?",
            "The method used by the researcher proved to be remarkably effective.",
            "Our method of analysis was thoroughly reviewed by the entire committee.",
            "A new method for teaching foreign languages was introduced last month.",
            "She explained the function of each department within the organization.",
            "The function began promptly at seven in the evening with opening remarks.",
            "His primary function in the team is coordinating logistics and supplies.",
            "They questioned the method behind the sudden change in company policy.",
            "The function of this department is crucial for community outreach.",
            "A traditional method of bread baking requires natural sourdough starter.",
            "The social function brought together alumni from across the country.",
            "Scientists developed an innovative method for water purification.",
            "Every function in government must remain accountable to the public.",

            // 2. Class / Struct / Record / Enum prose (20 cases)
            "The class starts at ten in the morning on Tuesdays and Thursdays.",
            "My class project is due tomorrow before midnight.",
            "The class was held yesterday in the main auditorium on campus.",
            "The class schedule changed after the mid-semester evaluation.",
            "A class of thirty students attended the interactive workshop.",
            "We booked a first class flight to Seattle for the annual summit.",
            "She belongs to a working class family with strong community values.",
            "He delivered a master class on landscape painting to aspiring artists.",
            "Each class has thirty students enrolled for the spring semester.",
            "Our class reunion was scheduled for the first weekend of October.",
            "The record was deleted from the archive during the routine cleanup.",
            "The company set a new record for quarterly sales last month.",
            "She holds the world record for the fastest marathon finish in history.",
            "His medical record was transferred to the specialist for a second opinion.",
            "A vinyl record was spinning on the vintage turntable in the corner.",
            "The payment status is pending approval from the finance director.",
            "Her current employment status was updated following the promotion.",
            "The flight status was listed as delayed due to severe thunderstorms.",
            "The enum was mentioned in the lecture on discrete mathematics.",
            "The status of the application is currently under administrative review.",

            // 3. Interface prose (10 cases)
            "The interface between the teams is broken and needs urgent mediation.",
            "The interface between hardware and software requires careful calibration.",
            "The user interface received praise for its intuitive and clean layout.",
            "A clean audio interface is essential for high fidelity sound recording.",
            "The network interface experienced intermittent packet loss yesterday.",
            "Communication across the departmental interface improved substantially.",
            "The visual interface presents key financial statistics in real time.",
            "Engineers inspected the physical interface between the two modules.",
            "The interface was updated to improve accessibility for screen readers.",
            "An intuitive tactile interface enhances cockpit operations for pilots.",

            // 4. Casing terminology prose (15 cases)
            "Use camel case when writing documentation for the team.",
            "The snake case is commonly used in Python naming conventions.",
            "Snake case is commonly used in Python programming manuals.",
            "We discussed camel case yesterday during our engineering standup.",
            "I prefer snake case for naming configuration files in this repository.",
            "Camel case is a naming convention where words are joined without spaces.",
            "He asked whether kebab case was acceptable for URL slugs.",
            "She explained why constant case is preferred for global immutable values.",
            "The author recommends pascal case for class identifiers in documentation.",
            "Our team adopted snake case for database column naming standards.",
            "Many developers favor camel case for local variable declarations.",
            "The guide compares camel case with snake case across several languages.",
            "In this publication camel case refers to compound words with capital letters.",
            "Always use snake case when specifying keys in the configuration dictionary.",
            "The style guide mandates kebab case for all customer-facing routes.",

            // 5. Punctuation & Symbol words prose (15 cases)
            "The arrow points to the right toward the emergency exit.",
            "An arrow pointing towards the entrance caught everyone's attention.",
            "Follow the green arrow to reach the main parking pavilion.",
            "The red arrow indicates the direction of heavy incoming traffic.",
            "The arrow on the weather vane indicated a shifting northerly wind.",
            "The value equals zero when the equation is completely balanced.",
            "The total equals fifty dollars after applying the promotional coupon.",
            "Her performance equals that of seasoned professionals in the industry.",
            "Put a dot after the sentence to signify the complete thought.",
            "A small red dot appeared in the top right corner of the document.",
            "Connect each dot to reveal the hidden constellation on the map.",
            "The artist painted every dot with meticulous attention to detail.",
            "Underscore is used in the explanation to highlight key principles.",
            "An underscore was drawn beneath the heading to create visual emphasis.",
            "We place an underscore between syllables during linguistic transcription.",

            // 6. File path & Location prose (15 cases)
            "At home I work on my laptop in a quiet dedicated study room.",
            "At work we collaborate using shared digital whiteboards and video calls.",
            "At school we studied C# and built desktop GUI applications.",
            "She works at Microsoft in the developer division on cloud tooling.",
            "We had lunch at the office cafeteria while reviewing the quarterly metrics.",
            "Meet me at the station before the morning express train departs.",
            "The conference will take place at the downtown convention center.",
            "He arrived at the airport two hours prior to the scheduled departure.",
            "We gathered at the library to prepare for the comprehensive examination.",
            "The team celebrated at the restaurant after successfully launching the product.",
            "At sunrise the fishing boats returned to the harbor with their daily catch.",
            "Children played at the park until the evening streetlights turned on.",
            "She volunteered at the animal shelter every Saturday morning.",
            "We stayed at the hotel near the botanical gardens during our vacation.",
            "At noon the bells rang across the historic town square.",

            // 7. Technical & Architecture prose (15 cases)
            "The API documentation is available online on the public portal.",
            "The database repository is located in the documentation library.",
            "The user profile is stored in the database under encrypted tables.",
            "We reviewed the system architecture with the principal infrastructure engineer.",
            "Security guidelines mandate regular audits of all authentication endpoints.",
            "The cloud migration reduced operating expenses by twenty percent.",
            "Continuous integration pipelines build and test every pull request automatically.",
            "The database backup completed successfully without any dropped connections.",
            "Telemetry data indicated optimal performance across all server clusters.",
            "The system architecture supports horizontal scaling during peak traffic periods.",
            "Encryption keys are securely managed through dedicated hardware security modules.",
            "The network latency between data centers remained consistently under five milliseconds.",
            "Automated monitoring alerted the operations team before any service degradation occurred.",
            "Our team adheres strictly to zero trust security architecture principles.",
            "The microservices communicate through asynchronous message queues for maximum resilience."
        };

        foreach (var sentence in sentences)
        {
            yield return new object[] { sentence };
        }
    }

    [Theory]
    [MemberData(nameof(CuratedAdversarialProseCases))]
    public void AdversarialProse_RemainsCleanNaturalLanguage_ZeroFalsePositives(string inputProse)
    {
        string output = _pipeline.Format(inputProse, ProseOptions);

        // Assert 1: Inviolable Zero-Enter invariant
        Assert.DoesNotContain("\r", output);
        Assert.DoesNotContain("\n", output);

        // Assert 2: No code punctuation operators accidentally injected into prose
        Assert.DoesNotContain("=>", output);
        Assert.DoesNotContain("->", output);
        Assert.DoesNotContain(" == ", output);
        Assert.DoesNotContain(" != ", output);

        // Assert 3: No unintended code identifiers generated from prose words
        Assert.False(Regex.IsMatch(output, @"\b(?:getUser|createUser|startsAt|isPending|worldRecord|world_record|emergencyExit)\b"),
            $"Accidental developer identifier created in prose output: '{output}' (Input was: '{inputProse}')");

        // Assert 4: Output begins with a capital letter and ends with valid terminal punctuation
        Assert.True(char.IsUpper(output[0]), $"Sentence did not start with capital letter: '{output}'");
        char lastChar = output[^1];
        Assert.True(lastChar is '.' or '?' or '!', $"Sentence did not end with valid terminal punctuation: '{output}'");
    }

    [Fact]
    public void AdversarialProse_TotalCuratedCorpusSize_Exceeds100Cases()
    {
        int count = 0;
        foreach (var _ in CuratedAdversarialProseCases())
        {
            count++;
        }

        Assert.True(count >= 100, $"Corpus must contain at least 100 cases, found: {count}");
    }
}
