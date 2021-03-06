﻿using System;

/// <summary>
/// Copyright © 2019 Anton Valkenberg
/// Written by BSc. A.J.J. Valkenberg, used in his Master Thesis on Artificial Intelligence.
/// In parts inspired by a code framework written by MSc. G.-J. Roelofs, MSc. T. Aliyev and MSc. D. de Rydt.
/// </summary>
namespace AVThesis.Search {

    /// <summary>
    /// Defines what an ensemble-strategy should have when searching with a SearchContext.
    /// </summary>
    /// <typeparam name="D"><see cref="SearchContext{D}"/></typeparam>
    /// <typeparam name="P"><see cref="SearchContext{P}"/></typeparam>
    /// <typeparam name="A"><see cref="SearchContext{A}"/></typeparam>
    /// <typeparam name="S"><see cref="SearchContext{S}"/></typeparam>
    /// <typeparam name="Sol"><see cref="SearchContext{Sol}"/></typeparam>
    public interface IEnsembleStrategy<D, P, A, S, Sol> where D : class where P : State where A : class where S : class where Sol : class {

        /// <summary>
        /// Creates an ensemble of searches based on the provided search function.
        /// </summary>
        /// <param name="context">The current search context.</param>
        /// <param name="searchFunction">The function that runs the actual search.</param>
        /// <param name="ensembleSize">The size of the ensemble.</param>
        void EnsembleSearch(SearchContext<D, P, A, S, Sol> context, Func<SearchContext<D, P, A, S, Sol>, A> searchFunction, int ensembleSize);

    }

}
