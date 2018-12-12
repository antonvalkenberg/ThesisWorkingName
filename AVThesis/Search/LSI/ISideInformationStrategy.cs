﻿/// <summary>
/// Written by A.J.J. Valkenberg, used in his Master Thesis on Artificial Intelligence.
/// In parts inspired by a code framework written by G.J. Roelofs and T. Aliyev.
/// </summary>
namespace AVThesis.Search.LSI {

    /// <summary>
    /// Defines what a strategy should have that creates side information.
    /// </summary>
    /// <typeparam name="D"><see cref="SearchContext{D}"/></typeparam>
    /// <typeparam name="P"><see cref="SearchContext{P}"/></typeparam>
    /// <typeparam name="A"><see cref="SearchContext{A}"/></typeparam>
    /// <typeparam name="S"><see cref="SearchContext{S}"/></typeparam>
    /// <typeparam name="Sol"><see cref="SearchContext{Sol}"/></typeparam>
    /// <typeparam name="T">The Type of side information that the search uses.</typeparam>
    public interface ISideInformationStrategy<D, P, A, S, Sol, T> where D : class where P : State where A : class where S : class where Sol : class where T : class {

        /// <summary>
        /// Creates the side information.
        /// </summary>
        /// <param name="context">The context of the search.</param>
        /// <param name="samplesForGeneration">The amount of samples used for generation of the side information.</param>
        /// <returns><typeparamref name="T"/> containing the side information.</returns>
        T Create(SearchContext<D, P, A, S, Sol> context, int samplesForGeneration);

    }

}
