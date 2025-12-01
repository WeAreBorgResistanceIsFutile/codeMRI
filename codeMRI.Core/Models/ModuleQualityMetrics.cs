namespace codeMRI.Core.Models
{
    /// <summary>
    /// Represents quality metrics for a module
    /// </summary>
    public class ModuleQualityMetrics
    {
        /// <summary>
        /// Measures how strongly related and focused the responsibilities of a single module are.
        /// Range: 0.0 to 1.0 (higher is better)
        /// </summary>
        public double Cohesion { get; set; }

        /// <summary>
        /// Measures the degree of interdependence between modules.
        /// Range: 0.0 to 1.0 (lower is better)
        /// </summary>
        public double Coupling { get; set; }

        /// <summary>
        /// Measures the complexity of the module (e.g., Cyclomatic Complexity).
        /// </summary>
        public double Complexity { get; set; }

        /// <summary>
        /// An instability metric (I = Ce / (Ca + Ce)).
        /// Range: 0.0 (stable) to 1.0 (unstable)
        /// </summary>
        public double Instability { get; set; }

        /// <summary>
        /// Abstractness metric (A = Na / Nc).
        /// Range: 0.0 (concrete) to 1.0 (abstract)
        /// </summary>
        public double Abstractness { get; set; }

        /// <summary>
        /// Distance from the Main Sequence (D = |A + I - 1|).
        /// Range: 0.0 (optimal) to 1.0 (problematic)
        /// </summary>
        public double DistanceFromMainSequence { get; set; }

        /// <summary>
        /// Overall maintainability index.
        /// Range: 0 to 100 (higher is better)
        /// </summary>
        public double MaintainabilityIndex { get; set; }
    }
}
