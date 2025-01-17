﻿using AutoMapper;
using Doctrina.Application.Common.Interfaces;
using Doctrina.Domain.Entities;
using Doctrina.ExperienceApi.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Doctrina.Application.Statements.Queries
{
    /// <summary>
    /// Request a single statement
    /// </summary>
    public class StatementQuery : IRequest<Statement>
    {
        public Guid StatementId { get; set; }
        public bool IncludeAttachments { get; set; }
        public ResultFormat Format { get; set; }

        public static StatementQuery Create(Guid statementId, bool includeAttachments = false, ResultFormat format = ResultFormat.Exact)
        {
            return new StatementQuery()
            {
                StatementId = statementId,
                IncludeAttachments = includeAttachments,
                Format = format
            };
        }

        public class Handler : IRequestHandler<StatementQuery, Statement>
        {
            private readonly IDoctrinaDbContext _context;
            private readonly IMapper _mapper;

            public Handler(IDoctrinaDbContext context, IMapper mapper)
            {
                _context = context;
                _mapper = mapper;
            }

            public async Task<Statement> Handle(StatementQuery request, CancellationToken cancellationToken)
            {
                var query = _context.Statements
                        .Where(x => x.StatementId == request.StatementId && x.Voided == false);

                if (request.IncludeAttachments)
                {
                    query = query.Include(x => x.Attachments)
                        .Select(x => new StatementEntity()
                        {
                            StatementId = x.StatementId,
                            FullStatement = x.FullStatement,
                            Attachments = x.Attachments
                        });
                }
                else
                {
                    query = query.Select(x => new StatementEntity()
                    {
                        StatementId = x.StatementId,
                        FullStatement = x.FullStatement
                    });
                }

                StatementEntity statementEntity = await query.FirstOrDefaultAsync(cancellationToken);

                if (statementEntity == null)
                {
                    return null;
                }

                return _mapper.Map<Statement>(statementEntity);
            }
        }
    }
}
