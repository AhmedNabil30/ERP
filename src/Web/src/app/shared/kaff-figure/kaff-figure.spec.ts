import { isZeroAmount, sameAmount, splitSigned } from './amount';

describe('amount helpers', () => {
  it('treats every spelling of zero as zero', () => {
    for (const zero of ['0', '0.00', '-0.0000', '+0', '000.000']) {
      expect(isZeroAmount(zero)).toBe(true);
    }
    expect(isZeroAmount('0.01')).toBe(false);
    expect(isZeroAmount('-0.0001')).toBe(false);
  });

  it('splits the sign from the magnitude and never signs a zero', () => {
    expect(splitSigned('-60000.0000')).toEqual({ negative: true, magnitude: '60000.0000' });
    expect(splitSigned('+5')).toEqual({ negative: false, magnitude: '5' });
    expect(splitSigned('-0.0000').negative).toBe(false);
  });

  it('compares amounts that differ only in trailing zeros', () => {
    expect(sameAmount('120000', '120000.0000')).toBe(true);
    expect(sameAmount('-1.50', '-1.5')).toBe(true);
    expect(sameAmount('119999.99', '120000.00')).toBe(false);
    expect(sameAmount('0.0000', '-0')).toBe(true);
  });
});
