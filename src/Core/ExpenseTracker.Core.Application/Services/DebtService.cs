using ExpenseTracker.Core.Application.DTOs.Debts;
using ExpenseTracker.Core.Application.Helpers;
using ExpenseTracker.Core.Application.Interfaces;
using ExpenseTracker.Core.Domain.Entities;

namespace ExpenseTracker.Core.Application.Services;

public class DebtService
{
    private const int MaxRowsWhenNeverPaidOff = 120;

    private readonly IDebtRepository _debts;
    private readonly CurrencyService _currency;
    private readonly IDateProvider _dateProvider;

    public DebtService(IDebtRepository debts, CurrencyService currency, IDateProvider dateProvider)
    {
        _debts = debts;
        _currency = currency;
        _dateProvider = dateProvider;
    }

    // ---------- Debts ----------

    /// <summary>Summary of every debt: balance, % paid, installment, next payment and estimated payoff.</summary>
    public async Task<List<DebtDto>> GetSummaryAsync()
    {
        var debts = await _debts.GetAllWithPaymentsAsync();
        var converter = await _currency.GetConverterAsync();
        var today = _dateProvider.Today;

        return debts
            .OrderBy(d => d.IsClosed)
            .ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(d => ToDto(d, today, converter))
            .ToList();
    }

    public async Task<DebtDetailDto> GetDetailAsync(int id)
        => await BuildDetailAsync(await GetWithPaymentsOrThrowAsync(id));

    /// <summary>Same as <see cref="GetDetailAsync"/>, but returns null when the debt does not exist (or is not yours).</summary>
    public async Task<DebtDetailDto?> TryGetDetailAsync(int id)
    {
        var debt = await _debts.GetWithPaymentsAsync(id);
        return debt is null ? null : await BuildDetailAsync(debt);
    }

    private async Task<DebtDetailDto> BuildDetailAsync(Debt debt)
    {
        var converter = await _currency.GetConverterAsync();

        return new DebtDetailDto
        {
            Debt = ToDto(debt, _dateProvider.Today, converter),
            Payments = debt.Payments
                .OrderByDescending(p => p.Date).ThenByDescending(p => p.Id)
                .Select(ToDto)
                .ToList()
        };
    }

    public async Task<DebtDto> CreateAsync(DebtInput input)
    {
        Validate(input);

        var debt = new Debt();
        Apply(debt, input);
        debt.IsClosed = debt.GetRemainingBalance() <= 0;
        await _debts.AddAsync(debt);

        return ToDto(debt, _dateProvider.Today, await _currency.GetConverterAsync());
    }

    public async Task<DebtDto> UpdateAsync(int id, DebtInput input)
    {
        Validate(input);

        var debt = await GetWithPaymentsOrThrowAsync(id);
        Apply(debt, input);
        debt.IsClosed = debt.GetRemainingBalance() <= 0;
        await _debts.UpdateAsync(debt);

        return ToDto(debt, _dateProvider.Today, await _currency.GetConverterAsync());
    }

    /// <summary>Deletes the debt and, in cascade, all its payments.</summary>
    public async Task DeleteAsync(int id)
    {
        var debt = await _debts.GetByIdAsync(id) ?? throw new InvalidOperationException("La deuda no existe.");
        await _debts.DeleteAsync(debt);
    }

    // ---------- Payments ----------

    /// <summary>Registers a payment, splitting it into interest and principal (rule 5.4).</summary>
    public async Task<DebtPaymentDto> RegisterPaymentAsync(int debtId, DebtPaymentInput input)
    {
        if (input.Amount <= 0)
            throw new ArgumentException("El monto debe ser mayor que cero.");

        var debt = await GetWithPaymentsOrThrowAsync(debtId);
        if (debt.IsClosed)
            throw new InvalidOperationException("La deuda ya está saldada.");

        var balance = debt.GetRemainingBalance();
        var interestDue = AmortizationCalculator.MonthlyInterest(balance, debt.AnnualInterestRate);
        if (input.Amount > balance + interestDue)
            throw new InvalidOperationException("El pago no puede superar el saldo pendiente más los intereses.");

        var (interest, principal) = AmortizationCalculator.SplitPayment(input.Amount, balance, debt.AnnualInterestRate);

        var payment = new DebtPayment
        {
            DebtId = debt.Id,
            Amount = input.Amount,
            Date = input.Date,
            InterestPortion = interest,
            PrincipalPortion = principal,
            Notes = string.IsNullOrWhiteSpace(input.Notes) ? null : input.Notes.Trim()
        };
        await _debts.AddPaymentAsync(payment);

        if (balance - principal <= 0)
        {
            debt.IsClosed = true;
            await _debts.UpdateAsync(debt);
        }

        return ToDto(payment);
    }

    /// <summary>Removes a payment; the balance is recalculated and a settled debt is reopened if needed.</summary>
    public async Task DeletePaymentAsync(int debtId, int paymentId)
    {
        var debt = await GetWithPaymentsOrThrowAsync(debtId);
        var payment = debt.Payments.FirstOrDefault(p => p.Id == paymentId)
            ?? throw new InvalidOperationException("El pago no existe.");

        var balanceAfterDelete = debt.GetRemainingBalance() + payment.PrincipalPortion;
        await _debts.DeletePaymentAsync(paymentId);

        var shouldBeClosed = balanceAfterDelete <= 0;
        if (debt.IsClosed != shouldBeClosed)
        {
            debt.IsClosed = shouldBeClosed;
            await _debts.UpdateAsync(debt);
        }
    }

    // ---------- Amortization ----------

    /// <summary>Projected installments from now until the balance reaches 0.</summary>
    public async Task<AmortizationDto> GetAmortizationAsync(int debtId)
    {
        var debt = await GetWithPaymentsOrThrowAsync(debtId);
        var schedule = AmortizationCalculator.ForDebt(debt, _dateProvider.Today);

        var truncated = !schedule.PaysOff && schedule.Rows.Count > MaxRowsWhenNeverPaidOff;
        return new AmortizationDto
        {
            Rows = truncated ? schedule.Rows.Take(MaxRowsWhenNeverPaidOff).ToList() : schedule.Rows,
            PaysOff = schedule.PaysOff,
            Truncated = truncated
        };
    }

    // ---------- Helpers ----------

    private async Task<Debt> GetWithPaymentsOrThrowAsync(int id)
        => await _debts.GetWithPaymentsAsync(id) ?? throw new InvalidOperationException("La deuda no existe.");

    private void Validate(DebtInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw new ArgumentException("El nombre es obligatorio.");
        if (!Enum.IsDefined(input.DebtType) || !Enum.IsDefined(input.Currency))
            throw new ArgumentException("El tipo de deuda o la moneda no son válidos.");
        if (input.OriginalAmount <= 0)
            throw new ArgumentException("El monto original debe ser mayor que cero.");
        if (input.InitialPaidAmount < 0)
            throw new ArgumentException("El monto pagado no puede ser negativo.");
        if (input.InitialPaidAmount >= input.OriginalAmount)
            throw new ArgumentException("El monto ya pagado debe ser menor que el monto original.");
        if (input.AnnualInterestRate < 0 || input.AnnualInterestRate > 100)
            throw new ArgumentException("La tasa anual debe estar entre 0 y 100.");
        if (input.PaymentDay < 1 || input.PaymentDay > 28)
            throw new ArgumentException("El día de pago debe estar entre 1 y 28.");
        if (input.MonthlyPayment < 0)
            throw new ArgumentException("La cuota no puede ser negativa.");
        if (input.TermMonths is < 1)
            throw new ArgumentException("El plazo debe ser de al menos 1 mes.");
        if (input.MonthlyPayment <= 0 && input.TermMonths is null)
            throw new ArgumentException("Indique la cuota mensual o el plazo en meses.");
    }

    private void Apply(Debt debt, DebtInput input)
    {
        debt.Name = input.Name.Trim();
        debt.DebtType = input.DebtType;
        debt.Currency = input.Currency;
        debt.OriginalAmount = input.OriginalAmount;
        debt.AnnualInterestRate = input.AnnualInterestRate;
        debt.InitialPaidAmount = input.InitialPaidAmount;
        debt.PaymentDay = input.PaymentDay;
        debt.StartDate = input.StartDate;
        debt.TermMonths = input.TermMonths;

        // With a term and no installment, the installment is calculated on the remaining balance.
        if (input.MonthlyPayment > 0)
        {
            debt.MonthlyPayment = input.MonthlyPayment;
        }
        else
        {
            var remaining = input.OriginalAmount - input.InitialPaidAmount;
            var today = _dateProvider.Today;
            var elapsed = Math.Max(0, (today.Year - input.StartDate.Year) * 12 + today.Month - input.StartDate.Month);
            var monthsLeft = Math.Max(1, input.TermMonths!.Value - elapsed);
            debt.MonthlyPayment = AmortizationCalculator.CalculateInstallment(remaining, input.AnnualInterestRate, monthsLeft);
        }
    }

    private static DebtDto ToDto(Debt d, DateOnly today, CurrencyConverter converter)
    {
        var balance = d.GetRemainingBalance();
        var schedule = AmortizationCalculator.ForDebt(d, today);
        var interestDue = AmortizationCalculator.MonthlyInterest(balance, d.AnnualInterestRate);

        return new DebtDto
        {
            Id = d.Id,
            Name = d.Name,
            DebtType = d.DebtType,
            Currency = d.Currency,
            OriginalAmount = d.OriginalAmount,
            AnnualInterestRate = d.AnnualInterestRate,
            InitialPaidAmount = d.InitialPaidAmount,
            MonthlyPayment = d.MonthlyPayment,
            PaymentDay = d.PaymentDay,
            StartDate = d.StartDate,
            TermMonths = d.TermMonths,
            IsClosed = d.IsClosed,
            RemainingBalance = balance,
            RemainingBalanceInBase = converter.ToBase(Math.Max(balance, 0m), d.Currency),
            PaidAmount = d.OriginalAmount - balance,
            PaidPercent = d.OriginalAmount > 0 ? Math.Round((d.OriginalAmount - balance) / d.OriginalAmount * 100m, 2) : 0m,
            NextPaymentDate = schedule.Rows.Count > 0 ? schedule.Rows[0].Date : null,
            EstimatedPayoffDate = schedule.PayoffDate,
            SuggestedPayment = d.IsClosed ? 0m : Math.Min(d.MonthlyPayment, balance + interestDue)
        };
    }

    private static DebtPaymentDto ToDto(DebtPayment p) => new()
    {
        Id = p.Id,
        Amount = p.Amount,
        Date = p.Date,
        InterestPortion = p.InterestPortion,
        PrincipalPortion = p.PrincipalPortion,
        Notes = p.Notes
    };
}
