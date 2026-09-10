using Expenses.Domain;
using Expenses.Domain.Expenses.Entities;
using POS.Shared.Application.IService;
using POS.Shared.Application.Messaging;
using POS.Shared.Domain;

namespace Expenses.Application.Expenses.Commands.CreateExpense
{
    internal sealed class CreateExpenseCommandHandler : ICommandHandler<CreateExpenseCommand, Guid>
    {
        private readonly IExpensesUnitOfWork _unitOfWork;
        private readonly ICacheService _cacheService;

        public CreateExpenseCommandHandler(IExpensesUnitOfWork unitOfWork, ICacheService cacheService)
        {
            _unitOfWork = unitOfWork;
            _cacheService = cacheService;
        }

        public async Task<Result<Guid>> Handle(CreateExpenseCommand request, CancellationToken cancellationToken)
        {
            var expenseResult = Expense.Create(
                request.Title, request.Amount, request.CreatedByUserId,
                request.Description, request.ExpenseDate, request.Notes);

            if (expenseResult.IsFailure)
                return Result<Guid>.Failure(expenseResult.Error);

            var expense = expenseResult.Value!;
            await _unitOfWork.ExpenseRepository.AddAsync(expense);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            await _cacheService.RemoveByPrefixAsync("dashboard_", cancellationToken);

            return Result<Guid>.Success(expense.Id);
        }
    }
}
